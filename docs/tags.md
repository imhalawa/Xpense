# Tags

A tag is a free-form label you attach to a transaction. Where a category is the single answer to
"what was this for?", tags are for everything that cuts across categories — `holiday-2026`,
`reimbursable`, `shared-with-sam`.

A transaction may carry any number of tags, or none.

## Fields

| Field | Notes |
| --- | --- |
| Label | Required, up to 100 characters |
| Background colour | Required, hex |
| Foreground colour | Required, hex |

Colours accept 3 or 6 hex digits, with or without a leading `#` — `#1a2b3c`, `1a2b3c` and `#abc`
are all valid. Anything else is rejected with a message that shows the expected shape. They are
stored without the `#`.

Create a tag with the **+** beside *Tags* in the sidebar. Tags you type into the transaction form
are created inline if they do not exist yet, so you do not have to define one before using it.

## Colour and readability

You pick both colours yourself rather than a single one, so a tag stays legible on both the light
and dark themes. The client checks the contrast between the pair when rendering the chip.

## Filtering by tag

Selecting a tag in the sidebar filters the transaction list to transactions carrying it. Tag
filters combine with the category, merchant, account and date filters.

## Deleting a tag

Deletion is soft. Transactions that carried the tag keep their history, and any filter currently
pointing at the deleted tag is dropped with a note saying so, rather than silently returning an
empty list.
