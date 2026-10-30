# Домашнє завдання — Frontend: екран Owner групового pre-order

Owner бачить, що обрав кожен гість, і підтверджує одне спільне замовлення для ресторану.
Код — у `frontend/restaurant-frontend-app`, бекенд для рівня 3 — мій, з бекенд-треку (`homework-backend.md`).

## Що зроблено

| Рівень | Що реалізовано |
|---|---|
| Практичне | `ParticipantCardComponent`: страви (фото або перша літера), кількість, ціна, Subtotal, статус кошика текстом; сторінка і маршрут `/reservations/:reservationId/group-order` |
| 🟢 1 | Верстка за макетом `01-owner-review.png`: шапка (адреса, дата, час, «N of M guests»), картки, Grand Total, кнопка Submit. Семантика `<main>`/`<h1>`/`<ul>`/`<article>`, кольори з `abstracts/colors`, екран не ламається на 375 px |
| 🔵 2 | `GroupOrdersService` (5 ендпоінтів). Стан — `signal()`, похідні — `computed()`. «My Pre-Order» для власної сесії, блок «Consolidated pre-order» через `consolidatePreOrder()`, **Grand Total = сума лише підтверджених кошиків**, жовта плашка про непідтверджених, бейдж «Collecting pre-orders» / «Sent to Restaurant», Submit через `<app-modal>` (подвійний клік не шле запит двічі), toast після успіху, read-only після відправки. Стани loading / error з «Try again» / «Group reservation not found» / empty |
| 🟣 3 | Фонова синхронізація кожні 15 с, поки група `Open` (`interval` + `exhaustMap`, новий запит не стартує, поки не відповів попередній), без лоадера; «Updated at …» і кнопка Refresh; при помилці — «Couldn’t refresh. Showing data from …» без toast (`HttpContext` + `SKIP_ERROR_TOAST`, `{ silent: true }`). Таймер не плодиться при «Try again», зупиняється після Submit і при виході зі сторінки (`takeUntilDestroyed`) |
| ⭐ Бонус | Remove Guest: кнопка лише в картках гостей (`canRemove`, `removeClick`, `aria-label="Remove <ім'я>"`), діалог підтвердження за макетом `03-remove-guest.png`, список і суми оновлюються з відповіді сервера |

Також: кнопка **Group Pre-Order** на картці бронювання (для бронювань на 2+ гостей) веде на цей екран; `backendUrl` у `environment.development.ts` вказує на мій API Gateway (`us-east-2`).

## Скріни станів екрана

Папка `homework-frontend-screenshots/`. Спершу знімки вікна Chrome на мок-бекенді (`npm start`, `?demo`) — вони показують стани, які важко відтворити наживо:

| Файл | Стан |
|---|---|
| `01-owner-review-top.webp` | огляд Owner, верх: шапка, бейдж «Collecting pre-orders», «Updated at …» і Refresh, жовта плашка, «My Pre-Order» |
| `02-owner-review-bottom.webp` | огляд Owner, низ: картки гостей із Remove Guest, консолідоване замовлення, Grand Total $50.50 (лише підтверджені кошики, а не $74.50 усіх) |
| `03-submit-dialog.webp` | діалог підтвердження Submit |
| `04a-sent-to-restaurant-top.webp` | після відправки: бейдж «Sent to Restaurant»; плашка, Refresh і Remove Guest зникли |
| `04b-sent-to-restaurant-bottom.webp` | після відправки: toast «Pre-order sent to the restaurant», неактивна кнопка «Pre-Order Sent» |
| `05-remove-guest-dialog.webp` | діалог Remove Guest з ім'ям гостя |
| `06-mobile.webp` | ширина телефона (400 px, DevTools device mode) |
| `07-error-try-again.webp` | помилка завантаження з кнопкою «Try again» (бронювання, якого немає в моку) |

Стан loading («Loading group pre-order…») на моках триває ~0,4 с, тому окремого скріна немає. Стан «Participants haven’t added any dishes yet» на демо-даних не відтворюється (у гостей уже є страви) — його перевіряє тест рівня 2.

Скріни з **реальним бекендом** (`npm start`, мій API у AWS, справжнє бронювання на 4 гостей):

| Файл | Стан |
|---|---|
| `real-1-owner-review-top.webp`, `real-2-owner-review-bottom.webp` | огляд Owner на реальних даних |
| `real-3-sync-new-guest.webp` | новий гість з'явився сам, без перезавантаження сторінки |
| `real-4-remove-guest-dialog.webp`, `real-5-guest-removed.webp` | Remove Guest: діалог і результат |
| `real-6-submit-dialog.webp` | діалог Submit, $75.47 |
| `real-7-sent-bottom.webp`, `real-8-sent-top.webp` | після відправки: toast, «Pre-Order Sent», «Sent to Restaurant» |

## Перевірка на справжньому бекенді

Рівень 3 і бонус пройдені наживо: реєстрація, бронювання, запрошення, гість із власним кошиком, pre-order власника, екран Group Pre-Order, синхронізація, видалення гостя, Submit. Після Submit у DynamoDB: група у статусі `Submitted`, замовлення `SubmittedByCustomer` з 4 стравами гостя і власника, видаленого гостя в сесіях немає.

Ця перевірка знайшла те, чого не бачили моки, — виправлено:

- **Фронтенд, `token.interceptor.ts`:** запити `/guest/groups/*` ішли без токена власника, тому API Gateway відповідав 401 і екран показував помилку. Також ігнорувався `SKIP_ERROR_TOAST`. Додано `group-orders.auth.spec.ts` (4 тести зі справжнім interceptor).
- **Фронтенд, `group-order-page.component.ts`:** сесія власника спершу створюється, а потім читається список учасників.
- **Бекенд, `GuestSessionService.cs`:** страви власника лежать у таблиці Orders, тому його картка була порожня, а Grand Total показував $33.98 замість $75.47. Бекенд розгорнуто в AWS, додано 3 тести (усього 167 із 167).

## Як перевірено

- `npm run test:hw` — **71 з 71** тестів зелені (практичне, рівні 1–3, бонус, `shared/helpers`, моки, авторизація Owner).
- `npx tsc -p tsconfig.app.json --noEmit` і `npx tsc -p tsconfig.spec.json --noEmit` — без помилок.
- `BACKEND_URL=https://example.com npm run build` — збірка успішна, «Bundle check passed», initial bundle 795 kB (ліміт 1 MB); сторінка — окремий lazy-чанк 20 kB.
- Вручну в браузері на мок-бекенді: усі сценарії зі скрінів, ширина 375 px без горизонтальної прокрутки.
- Вручну на справжньому бекенді: увесь шлях від бронювання до Submit, результат звірено в DynamoDB.

**Відомі обмеження справжнього бекенду** (код, що був в архіві до домашки): `GET /guest/invites/{token}` відмовляє, коли група заповнена, а `GET /reservations/{id}/invite-token` — коли група вже `Submitted`. Тому на реальних даних варто бронювати 3+ гостей і не перезавантажувати сторінку після Submit.

## Чи використовувався AI

Так. Код і скріни підготовлені з допомогою AI-асистента **Claude (Anthropic)**; тести домашки — від організаторів, без змін. Результат перевірено автоматичними тестами домашки, типізацією, продакшн-збіркою і вручну в браузері, зокрема на справжньому бекенді.
