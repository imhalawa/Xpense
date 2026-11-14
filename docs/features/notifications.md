# Notifications

Xpense can report when spending exceeds a budget. A budget's alert threshold is stored, but it does not currently produce a threshold notification.

For server-side financial records, the notifications worker processes events and its budget rule detects an over-limit crossing. For the client local projection, the browser derives alerts from exceeded budget periods and stores read marks in browser local storage. These derived alerts are recalculated and their read state is local to the browser. The two paths have different delivery and read-state behavior.

Invitations can also result in email when SMTP is enabled. With email disabled, the invitation link is returned to the creator for manual delivery.

See [events and notifications](../architecture/events.md) for the worker, retries and rule design, and [budgets](budgets.md) for budget calculations.
