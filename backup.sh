#!/usr/bin/env bash
# BadStar backup protocol. Run from Git Bash in the project folder:
#   ./backup.sh "what changed"
#
#   1. git     - scripts only (see .gitignore). Commit script work yourself first; this pushes every branch
#                to the RANDY drive (F:\BadStarBackup\git\BadStar.git) and the current branch to GitHub.
#   2. Plastic - models, scenes, prefabs, materials, ProjectSettings, Packages (see ignore.conf).
#                Checks in every pending change, including new files, to BadStar@AnyColourYouLike@cloud.
#   3. F:      - plain mirrors on RANDY: the Unity project (minus Library/Temp/Logs) and the Blender sources
#                in D:\BadStar. Plastic history itself stays in the cloud; F: holds the current files.
set -u
MSG="${1:-Backup $(date '+%Y-%m-%d %H:%M')}"
PROJ="D:/MobileProjects/BadStar"
BLEND="D:/BadStar"
DEST="F:/BadStarBackup"
CM="D:/client/cm.exe"
fail=0

cd "$PROJ" || exit 1
if [ ! -d "F:/" ]; then echo "RANDY (F:) is not connected - plug it in and run again."; exit 1; fi
mkdir -p "$DEST"

echo "== 1. git (scripts)"
n=$(git status --porcelain | wc -l)
[ "$n" -gt 0 ] && echo "   note: $n uncommitted script change(s) are NOT in git yet (they still go to Plastic and F:)"
[ -d "$DEST/git/BadStar.git" ] || git init --bare -q "$DEST/git/BadStar.git"
git remote get-url randy >/dev/null 2>&1 || git remote add randy "$DEST/git/BadStar.git"
git push -q randy --all && git push -q randy --tags && echo "   pushed all branches to RANDY" || { echo "   git push to RANDY FAILED"; fail=1; }
git -c http.sslBackend=schannel push -q BadStar HEAD && echo "   pushed $(git branch --show-current) to GitHub" || { echo "   git push to GitHub FAILED"; fail=1; }

echo "== 2. Plastic (models, scenes, settings)"
pending=$("$CM" status --short | wc -l)
if [ "$pending" -gt 0 ]; then
  echo "   checking in $pending change(s)..."
  "$CM" checkin --all --private -c="$MSG" && echo "   Plastic check-in done" || { echo "   Plastic check-in FAILED"; fail=1; }
else
  echo "   nothing to check in"
fi

echo "== 3. RANDY (F:) mirrors"
mirror() { # robocopy exit codes 0-7 are success
  MSYS_NO_PATHCONV=1 robocopy "$1" "$2" /MIR /R:1 /W:1 /NFL /NDL /NP /NJH "${@:3}" >/dev/null
  local rc=$?; if [ $rc -lt 8 ]; then echo "   $1 -> $2 ok"; else echo "   $1 -> $2 FAILED (robocopy $rc)"; fail=1; fi
}
mirror "D:\\MobileProjects\\BadStar" "F:\\BadStarBackup\\unity" /XD Library Temp Logs obj Build Builds UserSettings .vs .git .plastic
mirror "D:\\BadStar" "F:\\BadStarBackup\\blender"
date '+%Y-%m-%d %H:%M' > "$DEST/last_backup.txt"

[ $fail -eq 0 ] && echo "== backup complete" || echo "== backup finished WITH ERRORS (see above)"
exit $fail
