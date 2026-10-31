@api @group-preorder
Feature: Group pre-order rules

  The customer who booked a table (the owner) invites guests, each guest fills a cart of
  their own, and the owner confirms one consolidated pre-order for the restaurant. These
  scenarios cover who may do what and which requests are turned down.

  Background:
    Given a customer has booked a table for a group

  Rule: The owner sees who joined and what they chose

    @level1
    Scenario: Every participant is listed with their dishes and totals
      Given "Anna" has joined the group
      And "Anna" has added 2 "QA Salad" to their cart
      And "Anna" has confirmed their cart
      And "Ben" has joined the group
      And "Ben" has added 1 "QA Soup" to their cart
      When the owner asks for the participants
      Then the participants are
        | participant | dishes       | total | confirmed |
        | Anna        | QA Salad × 2 | 25.00 | yes       |
        | Ben         | QA Soup × 1  | 8.00  | no        |
      And the group total is 33.00

    @level1
    Scenario: The group total follows a guest's cart
      Given "Anna" has joined the group
      And "Anna" has added 1 "QA Soup" to their cart
      When "Anna" adds 1 "QA Pasta" to their cart
      And the owner asks for the participants
      Then the group total is 23.00

    @level1
    Scenario: A guest without dishes is still listed
      Given "Anna" has joined the group
      When the owner asks for the participants
      Then the participants are
        | participant | dishes | total | confirmed |
        | Anna        |        | 0.00  | no        |

    @level1
    Scenario: Nobody has joined yet
      When the owner asks for the participants
      Then the group has no participants

    @level1
    Scenario: The owner has a place of their own in the group
      When the owner takes their own place in the group
      And the owner asks for the participants
      Then the owner is listed as a participant

    @level1 @negative
    Scenario Outline: Only the owner sees the group
      Given "Anna" has joined the group
      When <caller> asks for the participants
      Then <caller> is <refusal>

      Examples: Who is turned down
        | caller           | refusal        |
        | another customer | refused        |
        | the waiter       | refused        |
        | a guest          | asked to sign in |
        | a stranger       | asked to sign in |

    @level1 @negative
    Scenario: A group that does not exist
      When the owner asks for the participants of a group that does not exist
      Then the owner is told it could not be found

  Rule: The owner confirms one consolidated pre-order

    @level2
    Scenario: Confirmed carts are merged and unconfirmed ones left out
      Given "Anna" has joined the group
      And "Anna" has added 2 "QA Salad" to their cart
      And "Anna" has confirmed their cart
      And "Ben" has joined the group
      And "Ben" has added 1 "QA Salad" to their cart
      And "Ben" has added 1 "QA Soup" to their cart
      And "Ben" has confirmed their cart
      And "Cara" has joined the group
      And "Cara" has added 1 "QA Pasta" to their cart
      And the owner has started their own order with 1 "QA Soup"
      When the owner confirms the pre-order
      Then the pre-order is accepted
      And the order holds
        | dish           |
        | QA Salad × 3   |
        | QA Soup × 2    |
      And the order has been handed to the restaurant
      And the group is marked as sent to the restaurant

    @level2 @negative
    Scenario: Nothing has been confirmed yet
      Given "Anna" has joined the group
      And "Anna" has added 1 "QA Soup" to their cart
      And the owner has started their own order with 1 "QA Soup"
      When the owner confirms the pre-order
      Then the owner is told "There are no submitted guest sessions in this group reservation."

    # A confirmed cart can never be empty: an empty cart cannot be confirmed in the first place, and a
    # confirmed cart cannot be changed. So the backend's "no dishes in confirmed carts" check is out of
    # reach, and this is the rule that keeps it that way.
    @level2 @negative
    Scenario: An empty cart cannot be confirmed
      Given "Anna" has joined the group
      When "Anna" confirms their cart
      Then the guest is turned down

    @level2 @negative
    Scenario: The owner has no order yet
      Given "Anna" has joined the group
      And "Anna" has added 1 "QA Soup" to their cart
      And "Anna" has confirmed their cart
      When the owner confirms the pre-order
      Then the owner is told "was not found"

    @level2 @negative
    Scenario: A group that does not exist cannot be confirmed
      When the owner confirms the pre-order of a group that does not exist
      Then the owner is told it could not be found

    @level2 @negative
    Scenario: A reservation that does not exist cannot be confirmed
      When the owner confirms the pre-order of a reservation that does not exist
      Then the owner is told it could not be found

    @level2 @negative
    Scenario: A pre-order cannot be confirmed twice
      Given a confirmed cart and the owner's order are ready
      And the owner has confirmed the pre-order
      When the owner confirms the pre-order
      Then the owner is told "Group reservation was submitted."

    @level2 @negative
    Scenario Outline: Only the owner confirms
      Given a confirmed cart and the owner's order are ready
      When <caller> confirms the pre-order
      Then <caller> is <refusal>

      Examples: Who is turned down
        | caller           | refusal          |
        | another customer | refused          |
        | the waiter       | refused          |
        | a guest          | asked to sign in |
        | a stranger       | asked to sign in |

  Rule: The owner can remove a participant

    @level3
    Scenario: Removing one of several participants
      Given "Anna" has joined the group
      And "Anna" has added 1 "QA Pasta" to their cart
      And "Ben" has joined the group
      And "Ben" has added 1 "QA Soup" to their cart
      When the owner removes "Ben"
      Then the removal is accepted
      And the participants are
        | participant | dishes        | total | confirmed |
        | Anna        | QA Pasta × 1  | 15.00 | no        |
      And the group total is 15.00

    @level3
    Scenario: Removing a guest who already confirmed
      Given "Anna" has joined the group
      And "Anna" has added 2 "QA Soup" to their cart
      And "Anna" has confirmed their cart
      And "Ben" has joined the group
      When the owner removes "Anna"
      Then the removal is accepted
      And the group total is 0.00

    @level3
    Scenario: Removing the last participant
      Given "Anna" has joined the group
      When the owner removes "Anna"
      Then the removal is accepted
      And the group has no participants

    @level3 @negative
    Scenario: A participant of another group cannot be removed
      Given "Anna" has joined the group
      And another customer has booked a table for a group called "Other"
      And "Omar" has joined the group called "Other"
      When the owner removes "Omar"
      Then the owner is told "Guest session does not belong to this group reservation."

    @level3 @negative
    Scenario: A participant who does not exist
      When the owner removes somebody who never joined
      Then the owner is told it could not be found

    @level3 @negative
    Scenario: The owner cannot be removed
      Given the owner has taken their own place in the group
      When the owner removes themselves
      Then the owner is told "The owner session cannot be removed from the group."

    @level3 @negative
    Scenario Outline: Only the owner removes participants
      Given "Anna" has joined the group
      When <caller> removes "Anna"
      Then <caller> is <refusal>

      Examples: Who is turned down
        | caller           | refusal          |
        | another customer | refused          |
        | the waiter       | refused          |
        | a guest          | asked to sign in |
        | a stranger       | asked to sign in |
