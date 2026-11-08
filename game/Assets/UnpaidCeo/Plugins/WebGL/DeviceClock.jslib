mergeInto(LibraryManager.library, {
  // Minutes east of UTC (New York in winter: -300). Unity's TimeZoneInfo.Local is always UTC in the web player.
  UnpaidCeo_UtcOffsetMinutes: function () {
    return -new Date().getTimezoneOffset();
  }
});
