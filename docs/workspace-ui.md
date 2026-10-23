# Workspace UI

HDD Index uses native Avalonia controls and the operating system's window chrome.
The neutral workspace follows the system's light/dark theme; blue is reserved for
navigation, linked-node indicators and the primary association action.

## Navigation

- **Repository** and the disk list select the browse destination.
- **浏览 / 整理** switch between a tree with a side inspector and two trees with a
  bottom inspector. The selected repository node is retained between modes.
- Search is explicitly scoped to **Repository**, including when a disk is being
  viewed. Previous/next results keep the existing wraparound behavior.
- The footer accepts a repository path. Press **Enter** to navigate or **Escape**
  to restore the current path. **Ctrl/⌘+L** focuses it; **Ctrl/⌘+F** focuses search.
- Storage-location rows open the exact indexed path in organize mode, including
  multiple locations on the same disk. The two automatic-location preferences
  remain independent.
- **Ctrl/⌘+S** and the toolbar Save button use the existing dirty-file save flow.
  Closing with unsaved edits still offers save, discard or cancel.

## Metadata, not filesystem operations

**复制目录结构** copies the selected disk node's index structure into the selected
virtual directory. **建立关联** validates the selected pair using the existing
holding strategy. Neither action moves or copies the underlying disk files.
Strategy changes retain the existing validation and confirmation dialogs.

Existing right-click commands remain available for creation, rename, delete,
search/delete, refresh (including skip-declared), expand-to-saved, declaration
removal and opening local paths. Local-path repair is available in disk browse.
No capacity, online-state or date data is invented by the interface.

## Implementation notes

Presentation state belongs to the existing view models and reuses `TreeProjection`
identities. Outline icons are native vector paths, not platform-specific symbol
fonts or rasterized mockups. Tree columns remain resizable and searchable by
keyboard; the repository name column retains sorting. Inspector locations scroll
independently, and organize trees have an adjustable splitter.

The startup details panel no longer forces the Windows-only Consolas family;
it inherits the application's existing font setup, matching the other dialogs.
