// The game log in the browser (the user, 2026-10-17: "the copy log doesn't work" there): Unity's clipboard doesn't reach a
// browser's, so the page copies the text itself and also hands it over as a download, in case the clipboard is refused.
mergeInto(LibraryManager.library, {
  GameLog_Copy: function (textPointer) {
    var text = UTF8ToString(textPointer);
    var copied = 0;
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text);
        copied = 1;
      } else {
        var area = document.createElement("textarea");
        area.value = text;
        area.style.position = "fixed";
        area.style.opacity = "0";
        document.body.appendChild(area);
        area.focus();
        area.select();
        copied = document.execCommand("copy") ? 1 : 0;
        document.body.removeChild(area);
      }
    } catch (e) {
      console.warn("Zeitgeist: the log couldn't be copied: " + e);
    }
    try {
      var blob = new Blob([text], { type: "text/plain;charset=utf-8" });
      var link = document.createElement("a");
      link.href = URL.createObjectURL(blob);
      link.download = "zeitgeist-game-log.txt";
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      setTimeout(function () { URL.revokeObjectURL(link.href); }, 1000);
    } catch (e) {
      console.warn("Zeitgeist: the log couldn't be downloaded: " + e);
    }
    return copied;
  }
});
