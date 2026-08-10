- Input workId
- Get branch name from workId, here named feature
	- Outputs a full reference name (e.g refs/head/{branchname})
- Get all pushes on that branch by passing fullRefName to GetPushIds as list
- Get all commits for each pushId

Vad har ändrats i vilket SSIS-paket eller SP

Jämför ny och gammal fil, extrahera vad som ändrats + 2-3 rader
Arbeta igenom commits kronologiskt, skriv inte över 