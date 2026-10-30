import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { EMPTY, Observable, Subscription, catchError, exhaustMap, filter, forkJoin, interval, map, switchMap, tap } from 'rxjs';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
import { GroupReservationStatus } from '@app/models/group-orders/group-status.model';
import { Participant } from '@app/models/group-orders/participant.model';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';
import { consolidatePreOrder } from '@app/shared/helpers/consolidate-pre-order';
import { LoadState } from './load-state.model';

/** How often the owner's screen pulls fresh carts while the group is open. */
export const GROUP_SYNC_INTERVAL_MS = 15_000;

@Component({
  selector: 'app-group-order-page',
  imports: [ParticipantCardComponent, ModalComponent, CurrencyPipe, DatePipe],
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent {
  private readonly groupOrders = inject(GroupOrdersService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  private loadSubscription?: Subscription;
  private syncSubscription?: Subscription;

  readonly reservationId = inject(ActivatedRoute).snapshot.paramMap.get('reservationId') ?? '';

  readonly loadState = signal<LoadState>('loading');
  readonly notFound = signal(false);
  readonly invite = signal<GuestInvite | null>(null);
  readonly participants = signal<Participant[]>([]);
  readonly ownerSessionId = signal('');
  readonly status = signal<GroupReservationStatus>('Open');
  readonly confirmOpen = signal(false);
  readonly submitting = signal(false);
  readonly lastUpdated = signal<Date | null>(null);
  readonly refreshFailed = signal(false);
  /** A sync request is in flight — a new one must not start until it answers. */
  readonly syncing = signal(false);
  /** Guest the owner is about to remove (confirmation dialog is open while set). */
  readonly removeTarget = signal<Participant | null>(null);
  readonly removing = signal(false);

  /** What goes to the restaurant: only submitted carts, merged by dish. */
  readonly consolidated = computed(() => consolidatePreOrder(this.participants()));
  readonly grandTotal = computed(() => this.consolidated().total);
  readonly pendingCount = computed(() => this.participants().filter((person) => !person.isSubmitted).length);
  readonly isEmpty = computed(() => this.participants().every((person) => person.items.length === 0));
  readonly isSent = computed(() => this.status() === 'Submitted');
  readonly canSubmit = computed(
    () => !this.isSent() && !this.submitting() && this.consolidated().items.length > 0,
  );
  readonly statusLabel = computed(() => {
    switch (this.status()) {
      case 'Submitted':
        return 'Sent to Restaurant';
      case 'Closed':
        return 'Closed';
      default:
        return 'Collecting pre-orders';
    }
  });
  readonly pendingMessage = computed(() => {
    const count = this.pendingCount();
    return count === 1
      ? '1 participant hasn’t submitted their cart yet — their dishes won’t be included in the order'
      : `${count} participants haven’t submitted their cart yet — their dishes won’t be included in the order`;
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.loadSubscription?.unsubscribe();
    this.stopSync();
    this.loadState.set('loading');
    this.notFound.set(false);

    this.loadSubscription = this.groupOrders
      .getGroupInvite$(this.reservationId)
      .pipe(
        switchMap((invite) => {
          const groupId = invite.GroupReservationId;
          return forkJoin({
            sessions: this.groupOrders.getSessions$(groupId),
            ownerSessionId: this.groupOrders.getOwnerSessionId$(this.reservationId),
            status: this.groupOrders.getStatus$(groupId),
          }).pipe(map((data) => ({ invite, ...data })));
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: ({ invite, sessions, ownerSessionId, status }) => {
          this.invite.set(invite);
          this.participants.set(sessions.participants);
          this.ownerSessionId.set(ownerSessionId);
          this.status.set(status.status);
          this.lastUpdated.set(new Date());
          this.refreshFailed.set(false);
          this.loadState.set('ready');
          this.startSync();
        },
        error: (error: unknown) => {
          this.notFound.set(error instanceof HttpErrorResponse && error.status === 404);
          this.loadState.set('error');
        },
      });
  }

  /** Sync right now (the Refresh button). Skipped while a sync request is still in flight. */
  refresh(): void {
    if (this.syncing() || this.loadState() !== 'ready') {
      return;
    }
    this.sync$().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }

  private startSync(): void {
    this.stopSync();
    if (this.isSent()) {
      return;
    }

    this.syncSubscription = interval(GROUP_SYNC_INTERVAL_MS)
      .pipe(
        filter(() => !this.syncing()),
        exhaustMap(() => this.sync$()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }

  private stopSync(): void {
    this.syncSubscription?.unsubscribe();
    this.syncSubscription = undefined;
  }

  /** Quiet background update: no full-screen loader, no error toast, keeps the last data on failure. */
  private sync$(): Observable<unknown> {
    const groupId = this.invite()?.GroupReservationId;
    if (!groupId) {
      return EMPTY;
    }

    this.syncing.set(true);
    return forkJoin({
      sessions: this.groupOrders.getSessions$(groupId, { silent: true }),
      status: this.groupOrders.getStatus$(groupId, { silent: true }),
    }).pipe(
      tap(({ sessions, status }) => {
        this.participants.set(sessions.participants);
        this.status.set(status.status);
        this.lastUpdated.set(new Date());
        this.refreshFailed.set(false);
        this.syncing.set(false);
        if (this.isSent()) {
          this.stopSync();
        }
      }),
      catchError(() => {
        this.refreshFailed.set(true);
        this.syncing.set(false);
        return EMPTY;
      }),
    );
  }

  canRemove(person: Participant): boolean {
    return person.type === 'Guest' && person.id !== this.ownerSessionId() && !this.isSent();
  }

  askRemove(person: Participant): void {
    this.removeTarget.set(person);
  }

  cancelRemove(): void {
    if (!this.removing()) {
      this.removeTarget.set(null);
    }
  }

  confirmRemove(): void {
    const groupId = this.invite()?.GroupReservationId;
    const person = this.removeTarget();
    if (!groupId || !person || this.removing()) {
      return;
    }

    this.removing.set(true);
    this.groupOrders
      .removeSession$(groupId, person.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (snapshot) => {
          this.participants.set(snapshot.participants);
          this.lastUpdated.set(new Date());
          this.removing.set(false);
          this.removeTarget.set(null);
          this.toast.success(`${person.name} was removed from the reservation`);
        },
        // the error toast is shown by tokenInterceptor; the list stays as it was
        error: () => {
          this.removing.set(false);
          this.removeTarget.set(null);
        },
      });
  }

  openConfirm(): void {
    if (this.canSubmit()) {
      this.confirmOpen.set(true);
    }
  }

  closeConfirm(): void {
    if (!this.submitting()) {
      this.confirmOpen.set(false);
    }
  }

  submit(): void {
    const groupId = this.invite()?.GroupReservationId;
    if (!groupId || !this.canSubmit()) {
      return;
    }

    this.submitting.set(true);
    this.groupOrders
      .submit$(groupId, this.reservationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.confirmOpen.set(false);
          this.status.set('Submitted');
          this.stopSync();
          this.toast.success('Pre-order sent to the restaurant');
        },
        // the error toast is shown by tokenInterceptor; keep the screen usable
        error: () => {
          this.submitting.set(false);
          this.confirmOpen.set(false);
        },
      });
  }
}
