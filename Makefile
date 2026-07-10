# SimpleHost — build & deploy helpers.
#
#   make build : publish the self-contained, double-clickable Windows package into dist/SimpleHost/
#   make copy  : copy that package to the Windows Desktop (C:\Users\INGENIOZ\Desktop\simplehost)
#
# "Bureau" is the French display name Windows shows for the Desktop; the folder on disk is
# named "Desktop", so that is what WIN_DEST points at.

DIST     := dist/SimpleHost
WIN_DEST := /mnt/c/Users/INGENIOZ/Desktop/simplehost

.PHONY: build copy

build:
	bash scripts/package-windows.sh

copy:
	@test -d "$(DIST)" || { echo "No $(DIST) — run 'make build' first."; exit 1; }
	rm -rf "$(WIN_DEST)"
	mkdir -p "$(WIN_DEST)"
	cp -r "$(DIST)/." "$(WIN_DEST)/"
	@echo "Copied to $(WIN_DEST)"
