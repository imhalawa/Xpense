---
status: accepted
date: 2026-08-05
---

# Budgets are independent of one another

Nothing rejects two **Budgets** that cover the same **Category** at the same time. Several may
overlap — in different **Currencies**, over different lengths, or over the very same days — and
there is no precedence rule deciding which one applies. **Spent** and **Remaining** belong to a
**Budget**, not to a **Category**, so each answers only for itself.

There is therefore no override concept, no "most specific wins", and no uniqueness constraint
beyond the obvious keys.

## Why

The alternative was arrived at first and then abandoned, which is worth recording because the
missing validation looks like an oversight.

The reasoning that produced it went: a **Budget** should have one **Remaining**, so exactly one
**Budget** may apply per **Category** and period, so "300 a month except 500 in December" needs an
override row plus a rule saying it wins. Adding weekly and yearly periods then required that rule
to decide which *length* an override displaced. Wanting a holiday budget over an arbitrary stretch
of days broke it entirely: a 20-day window contains no whole week and sits inside one month, so
"the shortest period fully containing it" answers nothing useful.

Every one of those knots came from the single-**Remaining** premise, and that premise was never a
requirement. It was carried over from rejecting **Tag**-scoped **Budgets**, where overlap really is
a problem — a **Tag** budget silently double-counts one **Expense** across several budgets that are
each claiming to be *the* answer for that spending. Two **Budgets** deliberately created on one
**Category** are not that. They are two intentions, and a user who writes both meant both.

Once **Remaining** belongs to a **Budget** rather than to a **Category**, overlap stops being a
contradiction to resolve and becomes data to report. A weekly guard under a monthly ceiling is
expressible. A holiday window alongside a monthly plan is expressible. Neither needs a rule,
because neither is competing to be the same number.

Keeping a set of **Budgets** coherent is the user's business. Xpense records intentions the way it
records movements: as stated, without arbitration.

## Considered options

**Exactly one Budget per Category, Currency and period, with one-off Budgets overriding recurring
ones.** Gives **Remaining** a single answer per **Category** and makes the December case one row.
Rejected because the precedence rule has no sound answer for arbitrary windows, and because it
forbids a weekly budget and a monthly budget coexisting — two things a user might reasonably want
at once.

**Reject any overlap at creation.** No precedence rule at all, and single-answer **Remaining**
guaranteed by a constraint rather than by logic. Rejected because changing one month becomes three
operations — end the recurring **Budget**, state the exception, start a new recurring one — with a
gap in coverage if the third is forgotten, and because it still forbids the weekly-plus-monthly
pair.

**A row materialised per period**, so editing one period is editing its row. Genuinely avoids
precedence and keeps per-period history. Rejected as machinery the problem did not require once
overlap stopped being an error: something must generate the rows, they can drift from the rule
that made them, and an ungenerated period needs defined behaviour.

## Consequences

A client cannot ask "what is my Groceries budget" and get one answer, because that question is not
well-formed here. It asks a **Budget** for its **Remaining**, or lists the **Budgets** on a
**Category**.

A user can create two contradictory **Budgets** and Xpense will report both without comment. That
is deliberate. If it ever proves to be a real problem, the fix is a warning in a client or a
notification, not a constraint here.

Nothing needs a precedence rule, so nothing needs a rule for what happens when the periods have
different lengths — which is what made arbitrary windows safe to allow.
