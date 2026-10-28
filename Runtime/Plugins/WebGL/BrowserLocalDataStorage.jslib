// BrowserLocalDataStorage 의 브라우저 쪽 구현. 모든 호출은 동기이고, 페이지가 저장소를 막았거나 용량이 찼을 때
// localStorage 가 예외를 던지므로 접근마다 감싸서 C# 에 상태값으로 돌려준다.
mergeInto(LibraryManager.library, {
	GeunedaLocalStorageIsAvailable: function () {
		try {
			var probe = "__geuneda_probe__";
			window.localStorage.setItem(probe, probe);
			window.localStorage.removeItem(probe);
			return 1;
		} catch (e) {
			return 0;
		}
	},

	GeunedaLocalStorageHasKey: function (key) {
		return window.localStorage.getItem(UTF8ToString(key)) !== null ? 1 : 0;
	},

	GeunedaLocalStorageRead: function (key) {
		var value = window.localStorage.getItem(UTF8ToString(key));

		if (value === null) {
			return 0;
		}

		var size = lengthBytesUTF8(value) + 1;
		var buffer = _malloc(size);
		stringToUTF8(value, buffer, size);
		return buffer;
	},

	GeunedaLocalStorageWrite: function (key, value) {
		try {
			window.localStorage.setItem(UTF8ToString(key), UTF8ToString(value));
			return 1;
		} catch (e) {
			return 0;
		}
	},

	GeunedaLocalStorageDelete: function (key) {
		window.localStorage.removeItem(UTF8ToString(key));
	}
});
