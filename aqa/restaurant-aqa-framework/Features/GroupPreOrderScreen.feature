@ui @group-preorder
Feature: Group pre-order screen

  The owner follows the group on one screen, removes a guest if needed and sends the
  consolidated pre-order to the restaurant, where the waiter picks it up.

  Rule: The owner sees what everybody chose

    Background:
      Given a customer has booked a table for a group

    @level1 @smoke
    Scenario: Participants, dishes and totals are shown
      Given "Anna" has joined the group
      And "Anna" has added 2 "QA Salad" to their cart
      And "Anna" has confirmed their cart
      And "Ben" has joined the group
      And "Ben" has added 1 "QA Soup" to their cart
      When the owner opens the group's screen
      Then "Guest: Anna" is shown as "Submitted" with subtotal "$25.00" and dishes
        | dish                |
        | QA Salad × 2 $25.00 |
      And "Guest: Ben" is shown as "Still choosing" with subtotal "$8.00" and dishes
        | dish               |
        | QA Soup × 1 $8.00 |
      And the owner is warned that 2 participants have not confirmed
      And the grand total shown is "$25.00"

    @level1
    Scenario: A guest's change reaches the owner's screen
      Given "Ben" has joined the group
      And "Ben" has added 1 "QA Soup" to their cart
      And the owner has opened the group's screen
      When "Ben" adds 1 "QA Pasta" to their cart
      And the owner refreshes the screen
      Then "Guest: Ben" is shown as "Still choosing" with subtotal "$23.00" and dishes
        | dish                |
        | QA Soup × 1 $8.00   |
        | QA Pasta × 1 $15.00 |

    @level1
    Scenario: A guest without dishes
      Given "Ben" has joined the group
      When the owner opens the group's screen
      Then "Guest: Ben" is shown as "Still choosing" without dishes

    @level1
    Scenario: Nobody has chosen anything yet
      When the owner opens the group's screen
      Then the owner is told nobody has added dishes yet
      And the owner cannot send the pre-order

    @level1 @negative
    Scenario: A reservation that does not exist
      When the owner opens the screen of a reservation that does not exist
      Then the owner is told the group could not be found

  Rule: The waiter gets what the owner sends

    Background:
      Given a customer has booked a table for a group that a waiter serves

    @level2 @sequential
    Scenario: The pre-order goes to the restaurant and the waiter sees it
      Given "Anna" has joined the group
      And "Anna" has added 2 "QA Salad" to their cart
      And "Anna" has confirmed their cart
      And "Ben" has joined the group
      And "Ben" has added 1 "QA Salad" to their cart
      And "Ben" has confirmed their cart
      And "Cara" has joined the group
      And "Cara" has added 1 "QA Pasta" to their cart
      And the owner has started their own order with 1 "QA Soup"
      And the owner has opened the group's screen
      Then the consolidated list shows
        | dish         |
        | QA Salad × 3 |
      When the owner sends the pre-order to the restaurant
      Then the owner is told "Pre-order sent to the restaurant"
      And the screen says "Sent to Restaurant"
      When the waiter opens the pre-order of this reservation
      Then the waiter sees
        | dish         |
        | QA Salad × 3 |
        | QA Soup × 1  |

    @level2 @sequential @level3
    Scenario: From the guests' carts to the waiter, without the removed guest
      Given "Anna" has joined the group
      And "Anna" has added 1 "QA Salad" to their cart
      And "Anna" has confirmed their cart
      And "Ben" has joined the group
      And "Ben" has added 2 "QA Pasta" to their cart
      And "Ben" has confirmed their cart
      And the owner has started their own order with 1 "QA Soup"
      And the owner has opened the group's screen
      When the owner removes "Ben" on the screen
      And the owner sends the pre-order to the restaurant
      And the screen says "Sent to Restaurant"
      And the waiter opens the pre-order of this reservation
      Then the waiter sees
        | dish         |
        | QA Salad × 1 |
        | QA Soup × 1  |

  Rule: The owner can only send confirmed carts

    Background:
      Given a customer has booked a table for a group

    @level2 @negative
    Scenario: Nothing to send until a cart is confirmed
      Given "Ben" has joined the group
      And "Ben" has added 1 "QA Soup" to their cart
      When the owner opens the group's screen
      Then the owner cannot send the pre-order

  Rule: The owner can remove a guest

    Background:
      Given a customer has booked a table for a group

    @level3
    Scenario: The remaining guests and the total are updated
      Given "Anna" has joined the group
      And "Anna" has added 1 "QA Pasta" to their cart
      And "Anna" has confirmed their cart
      And "Ben" has joined the group
      And "Ben" has added 1 "QA Soup" to their cart
      And "Ben" has confirmed their cart
      And the owner has opened the group's screen
      When the owner removes "Ben" on the screen
      Then "Guest: Ben" is no longer shown
      And the grand total shown is "$15.00"
