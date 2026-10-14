using System;
using System.Linq;
using System.Reflection;
using Geuneda.Services;
using Geuneda.Services.Editor.Explorer.Tabs;
using NUnit.Framework;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	/// <summary>
	/// 플레이가 끝난 뒤(도메인 리로드를 꺼서 정적 바인딩이 남은 경우 포함) Time 탭이 이전 서비스의 상태와 조작을 숨기는지 확인합니다.
	/// </summary>
	[TestFixture]
	public class TimeTabTest
	{
		[TearDown]
		public void Cleanup()
		{
			MainInstaller.Clean();
		}

		[Test]
		public void Refresh_InEditModeWithALeftoverBinding_HidesStateAndDisablesManipulation()
		{
			var time = new TimeService();
			time.SetServerTime(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
			MainInstaller.Bind<ITimeService>(time);
			MainInstaller.Bind<ITimeManipulator>(time);
			var tab = new TimeTab();

			Invoke(tab, "Refresh");

			AssertUnbound(tab);
		}

		[Test]
		public void OnExitingPlayMode_HidesStateAndDisablesManipulation()
		{
			var tab = new TimeTab();

			Invoke(tab, "OnExitingPlayMode");

			AssertUnbound(tab);
		}

		private static void AssertUnbound(TimeTab tab)
		{
			var labels = tab.Query<Label>().ToList().Select(label => label.text).ToList();
			var reset = tab.Query<Button>().ToList().Single(button => button.text == "Reset Time");

			CollectionAssert.Contains(labels, "ITimeService not bound");
			CollectionAssert.DoesNotContain(labels, "true (server)");
			Assert.IsFalse(reset.enabledSelf);
		}

		private static void Invoke(TimeTab tab, string method)
		{
			typeof(TimeTab).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tab, null);
		}
	}
}
