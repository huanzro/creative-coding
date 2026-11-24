mergeInto(LibraryManager.library, {
  EchoesPublish: function (message) {
    if (window.EchoesReceiveFrame) window.EchoesReceiveFrame(UTF8ToString(message));
  }
});
