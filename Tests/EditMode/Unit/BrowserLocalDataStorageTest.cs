using System;
using Geuneda.Services;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	[TestFixture]
	public class BrowserLocalDataStorageTest
	{
		[Test]
		public void Constructor_OutsideWebGlPlayer_ThrowsPlatformNotSupportedException()
		{
			// 에디터와 다른 플랫폼에서는 아무것도 저장하지 않은 채 조용히 동작하는 대신 생성을 거부한다
			Assert.Throws<PlatformNotSupportedException>(() => new BrowserLocalDataStorage());
		}
	}
}
