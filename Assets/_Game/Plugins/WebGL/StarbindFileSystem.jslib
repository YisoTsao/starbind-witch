// Mobile/desktop browser build: write the in-memory file system (persistentDataPath) back to
// IndexedDB right after a save, so closing the tab does not lose progress. Called by SaveService.
mergeInto(LibraryManager.library, {
  StarbindSyncFileSystem: function () {
    if (typeof FS !== "undefined" && FS.syncfs) {
      FS.syncfs(false, function (err) { if (err) console.warn("[Starbind] save sync failed", err); });
    }
  }
});
