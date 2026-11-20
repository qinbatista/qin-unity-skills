# Cache ownership

Run Workflow's current-project checkpoint at start and finish, then refresh Management's ignored `current.json` snapshot. Temporary plans, test fixtures, and candidate package copies use a registered `Cache/temp-*` run from the first write. After verified delivery, release the run and confirm removal. Preserve active consumers and exact recorded recovery inputs. Source and maintained tests stay versioned outside Cache.
