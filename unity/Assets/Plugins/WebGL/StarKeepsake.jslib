mergeInto(LibraryManager.library, {
  EchoesDownloadKeepsake: function (image, size) {
    if (!image || size <= 0) return;
    var blob = new Blob([HEAPU8.slice(image, image + size)], { type: "image/png" });
    var url = URL.createObjectURL(blob);
    var link = document.createElement("a");
    link.href = url;
    link.download = "Echoes-of-Light.png";
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
  }
});
