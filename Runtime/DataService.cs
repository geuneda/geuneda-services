using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// 플레이어의 저장된 영구 데이터에 접근하기 위한 인터페이스
	/// </summary>
	public interface IDataProvider
	{
		/// <summary>
		/// <typeparamref name="T"/> 타입의 플레이어 데이터를 요청합니다
		/// </summary>
		T GetData<T>() where T : class;

		/// <summary>
		/// 서비스가 <typeparamref name="T"/> 타입의 플레이어 데이터를 보유하고 있는지 확인합니다
		/// </summary>
		bool HasData<T>() where T : class;
	}

	/// <summary>
	/// 게임의 모든 영구 데이터를 관리하는 서비스입니다.
	/// 데이터 변경 시 박싱/언박싱 및 참조 손실이 발생하지 않도록 참조 타입만 사용합니다.
	/// </summary>
	public interface IDataService : IDataProvider
	{
		/// <summary>
		/// 게임의 <typeparamref name="T"/> 데이터를 디스크에 저장합니다
		/// </summary>
		void SaveData<T>() where T : class;

		/// <summary>
		/// 모든 게임 데이터를 디스크에 저장합니다
		/// </summary>
		void SaveAllData();

		/// <summary>
		/// 디스크에서 게임의 <typeparamref name="T"/> 데이터를 로드하여 반환합니다.
		/// 저장된 값이 없거나 읽을 수 없으면 새 인스턴스를 반환하며 예외를 던지지 않습니다.
		/// </summary>
		T LoadData<T>() where T : class;

		/// <summary>
		/// 주어진 <paramref name="data"/>를 메모리에 추가하거나 교체합니다.
		/// </summary>
		void AddOrReplaceData<T>(T data) where T : class;
	}

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// 데이터는 PlayerPrefs 에 타입의 <see cref="Type.FullName"/> 키로 <c>{"version":n,"data":{...}}</c> 형식의 JSON 으로 저장합니다.
	/// 버전은 데이터 타입의 <see cref="DataVersionAttribute"/> 값(없으면 0)입니다.
	/// </para>
	/// <para>
	/// 저장된 버전이 현재 버전보다 낮으면 <see cref="AddMigration{T}"/> 로 등록한 단계를 차례로 적용한 뒤 읽습니다.
	/// JSON 이 깨졌거나 형식이 다르거나 필요한 마이그레이션이 없으면 원문을 <c>{키}.corrupt</c> 에 남기고 에러 로그와
	/// <see cref="OnDataLoadFailed"/> 를 거쳐 새 인스턴스로 시작합니다. 새 인스턴스는 <see cref="SaveData{T}"/> 하기 전까지 원래 세이브를 덮어쓰지 않습니다.
	/// </para>
	/// <para>
	/// 저장된 버전이 현재 버전보다 높으면(더 새 앱이 저장) 알 수 있는 필드만 읽고, 원본을 잃지 않도록 그 타입은 저장하지 않습니다.
	/// 덮어쓰려면 <see cref="AddOrReplaceData{T}"/> 로 데이터를 직접 교체한 뒤 저장합니다.
	/// </para>
	/// </remarks>
	public class DataService : IDataService
	{
		/// <summary>
		/// 읽지 못한 세이브의 원문을 남겨 두는 PlayerPrefs 키의 접미사
		/// </summary>
		public const string CorruptKeySuffix = ".corrupt";

		private const string VersionProperty = "version";
		private const string DataProperty = "data";

		// 타입이 정해진 역직렬화에서도 날짜 형태 문자열을 DateTime 으로 바꾸지 않는다
		private static readonly JsonSerializerSettings ReadSettings = new JsonSerializerSettings
		{
			DateParseHandling = DateParseHandling.None
		};

		private readonly IDictionary<Type, object> _data = new Dictionary<Type, object>();
		private readonly Dictionary<(Type Type, int FromVersion), Func<JToken, JToken>> _migrations =
			new Dictionary<(Type Type, int FromVersion), Func<JToken, JToken>>();
		// 더 새 버전 세이브를 읽어 저장하지 않는 타입
		private readonly HashSet<Type> _newerVersionTypes = new HashSet<Type>();

		internal IReadOnlyDictionary<Type, object> DataEntries => (IReadOnlyDictionary<Type, object>)_data;

		/// <inheritdoc />
		public bool HasData<T>() where T : class
		{
			return _data.ContainsKey(typeof(T));
		}

		/// <inheritdoc />
		public T GetData<T>() where T : class
		{
			return _data[typeof(T)] as T;
		}

		/// <inheritdoc />
		public void SaveData<T>() where T : class
		{
			var type = typeof(T);
			var data = _data[type];

			if (SkipNewerVersion(type))
			{
				return;
			}

			var key = GetSaveKey(type);

			PlayerPrefs.SetString(key, Serialize(type, data));
			PlayerPrefs.Save();
			OnDataSaved(key, data, type);
		}

		/// <inheritdoc />
		public void SaveAllData()
		{
			foreach (var data in _data)
			{
				if (SkipNewerVersion(data.Key))
				{
					continue;
				}

				var key = GetSaveKey(data.Key);

				PlayerPrefs.SetString(key, Serialize(data.Key, data.Value));
				OnDataSaved(key, data.Value, data.Key);
			}
			
			PlayerPrefs.Save();
		}

		/// <inheritdoc />
		public T LoadData<T>() where T : class
		{
			var type = typeof(T);
			var key = GetSaveKey(type);
			var json = PlayerPrefs.GetString(key, "");
			var isNewerVersion = false;
			T instance;

			if (string.IsNullOrEmpty(json))
			{
				instance = Activator.CreateInstance<T>();
			}
			else if (!TryDeserialize(type, json, out instance, out isNewerVersion, out var error))
			{
				HandleLoadFailure(key, json, error);
				instance = Activator.CreateInstance<T>();
			}

			AddOrReplaceData(instance);

			// AddOrReplaceData 가 보호를 풀기 때문에 그 뒤에 표시한다
			if (isNewerVersion)
			{
				_newerVersionTypes.Add(type);
			}

			return instance;
		}

		/// <summary>
		/// <typeparamref name="T"/> 의 세이브를 <paramref name="fromVersion"/> 에서 <paramref name="fromVersion"/> + 1 로 바꾸는 단계를 등록합니다.
		/// <see cref="LoadData{T}"/> 는 저장된 버전부터 현재 버전(<see cref="DataVersionAttribute"/>)까지 등록된 단계를 차례로 적용합니다.
		/// </summary>
		/// <param name="fromVersion">이 단계가 받는 세이브 버전</param>
		/// <param name="migration"><c>data</c> 부분을 받아 다음 버전의 <c>data</c> 를 반환하는 함수. 받은 토큰을 고쳐서 그대로 반환해도 됩니다.
		/// 토큰의 실수는 가능하면 <see cref="decimal"/>, 날짜 형태 문자열은 문자열 그대로입니다</param>
		/// <exception cref="ArgumentNullException"><paramref name="migration"/> 이 null 인 경우</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromVersion"/> 이 음수이거나 현재 버전 이상인 경우</exception>
		/// <exception cref="ArgumentException">같은 버전의 단계를 이미 등록한 경우</exception>
		public void AddMigration<T>(int fromVersion, Func<JToken, JToken> migration) where T : class
		{
			if (migration == null)
			{
				throw new ArgumentNullException(nameof(migration));
			}

			var type = typeof(T);
			var currentVersion = GetDataVersion(type);

			if (fromVersion < 0 || fromVersion >= currentVersion)
			{
				throw new ArgumentOutOfRangeException(nameof(fromVersion), fromVersion,
					$"Migration source version must be between 0 and {currentVersion - 1} for {type.FullName}");
			}

			_migrations.Add((type, fromVersion), migration);
		}

		/// <summary>
		/// <paramref name="type"/> 데이터를 저장하는 PlayerPrefs 키
		/// </summary>
		public static string GetSaveKey(Type type)
		{
			return type.FullName;
		}

		/// <summary>
		/// <paramref name="type"/> 의 현재 세이브 스키마 버전(<see cref="DataVersionAttribute"/>, 없으면 0)
		/// </summary>
		public static int GetDataVersion(Type type)
		{
			return type.GetCustomAttribute<DataVersionAttribute>(false)?.Version ?? 0;
		}

		/// <inheritdoc />
		/// <remarks>
		/// 더 새 버전 세이브를 읽어 저장하지 않던 타입도, 이렇게 직접 교체하면 다시 저장합니다.
		/// </remarks>
		public void AddOrReplaceData<T>(T data) where T : class
		{
			_newerVersionTypes.Remove(typeof(T));

			if(HasData<T>())
			{
				_data[typeof(T)] = data;
			}
			else
			{
				_data.Add(typeof(T), data);
			}
		}

		protected virtual void OnDataSaved(string key, object data, Type type)
		{
		}

		/// <summary>
		/// 저장된 값을 읽지 못해 새 인스턴스로 시작할 때 호출됩니다. 원문은 <see cref="SaveCorruptBackup"/> 로도 남깁니다.
		/// 여기서 던진 예외는 로그로만 남고 로드는 계속됩니다.
		/// </summary>
		protected virtual void OnDataLoadFailed(string key, string rawData, Exception exception)
		{
		}

		/// <summary>
		/// 읽지 못한 세이브의 원문을 보관합니다. 기본은 PlayerPrefs 의 <paramref name="key"/> + <see cref="CorruptKeySuffix"/> 입니다.
		/// 여기서 던진 예외는 로그로만 남고 로드는 계속됩니다.
		/// </summary>
		protected virtual void SaveCorruptBackup(string key, string rawData)
		{
			PlayerPrefs.SetString(key + CorruptKeySuffix, rawData);
			PlayerPrefs.Save();
		}

		private bool SkipNewerVersion(Type type)
		{
			if (!_newerVersionTypes.Contains(type))
			{
				return false;
			}

			Debug.LogWarning($"Save data '{GetSaveKey(type)}' was loaded from a newer version and is not saved, to keep the original. " +
			                 "Replace it with AddOrReplaceData to overwrite.");

			return true;
		}

		// 백업과 실패 훅은 각각 실패해도 새 인스턴스로 시작하는 복구를 막지 않는다
		private void HandleLoadFailure(string key, string json, Exception error)
		{
			var backedUp = false;

			try
			{
				SaveCorruptBackup(key, json);
				backedUp = true;
			}
			catch (Exception backupException)
			{
				Debug.LogException(backupException);
			}

			Debug.LogError(backedUp
				? $"Failed to load save data '{key}'. The raw data was kept in '{key}{CorruptKeySuffix}' and a new instance is used.\n{error}"
				: $"Failed to load save data '{key}' and the raw data could not be backed up. A new instance is used.\n{error}");

			try
			{
				OnDataLoadFailed(key, json, error);
			}
			catch (Exception hookException)
			{
				Debug.LogException(hookException);
			}
		}

		private static string Serialize(Type type, object data)
		{
			return JsonConvert.SerializeObject(new SaveEnvelope { Version = GetDataVersion(type), Data = data });
		}

		private bool TryDeserialize<T>(Type type, string json, out T instance, out bool isNewerVersion, out Exception error)
			where T : class
		{
			instance = null;
			isNewerVersion = false;
			error = null;

			var currentVersion = GetDataVersion(type);
			// 헤더에서 정수 버전을 읽는 즉시 기록된다. 본문 검증이 실패해도 더 새 버전이면 저장을 막는 데 쓴다
			int? headerVersion = null;

			try
			{
				var savedVersion = ReadSavedVersion(json, ref headerVersion);

				if (savedVersion >= currentVersion)
				{
					if (savedVersion > currentVersion)
					{
						isNewerVersion = true;
						Debug.LogWarning($"Save data '{GetSaveKey(type)}' has version {savedVersion}, newer than {currentVersion}. " +
						                 "Unknown fields are ignored and this type is not saved until replaced.");
					}

					// 마이그레이션이 없으면 토큰을 거치지 않고 타입으로 바로 읽어 숫자 정밀도와 문자열을 그대로 둔다
					instance = JsonConvert.DeserializeObject<DataEnvelope<T>>(json, ReadSettings)?.Data;
				}
				else
				{
					var dataToken = ParseObject(json)[DataProperty];

					for (var version = savedVersion; version < currentVersion; version++)
					{
						if (!_migrations.TryGetValue((type, version), out var migration))
						{
							throw new InvalidOperationException(
								$"No migration registered for {type.FullName} from version {version} to {version + 1}");
						}

						dataToken = migration(dataToken) ??
							throw new InvalidOperationException($"Migration of {type.FullName} from version {version} returned null");
					}

					// 토큰을 바로 변환하면 decimal 토큰이 double 필드로 갈 때 값이 미세하게 달라질 수 있으므로,
					// 문자열로 다시 써서 타입으로 읽는다(필드 타입에 맞게 원문 리터럴을 정확히 읽음)
					instance = JsonConvert.DeserializeObject<T>(dataToken.ToString(Formatting.None), ReadSettings);
				}

				if (instance == null)
				{
					throw new JsonSerializationException($"Save data '{DataProperty}' is null");
				}

				return true;
			}
			catch (Exception e)
			{
				// 헤더에서 읽은 버전이 더 새 버전이면 본문을 읽지 못했어도 원본을 덮어쓰지 않도록 저장을 막는다
				error = e;
				instance = null;
				isNewerVersion = headerVersion.HasValue && headerVersion.Value > currentVersion;

				return false;
			}
		}

		/// <summary>
		/// 값을 변환하지 않고 최상위의 정수 <c>version</c> 과 <c>data</c> 가 있는지 확인하고 버전을 반환합니다.
		/// <paramref name="version"/> 은 정수 버전을 읽는 즉시 채워지므로, 뒤이은 본문 검증에서 예외가 나도 호출자가 볼 수 있습니다.
		/// </summary>
		private static int ReadSavedVersion(string json, ref int? version)
		{
			var hasData = false;

			using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
			{
				if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
				{
					throw new JsonSerializationException("Save data must be a JSON object");
				}

				while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
				{
					var name = (string)reader.Value;

					if (!reader.Read())
					{
						break;
					}

					if (name == VersionProperty && reader.TokenType == JsonToken.Integer)
					{
						version = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
					}
					else
					{
						hasData |= name == DataProperty;
						reader.Skip();
					}
				}

				if (reader.TokenType != JsonToken.EndObject)
				{
					throw new JsonSerializationException("Save data is not a complete JSON object");
				}

				// 봉투 뒤에 다른 내용(쓰레기 문자, 이어 붙인 봉투)이 있으면 읽지 못한 세이브로 본다. 공백은 허용한다
				if (reader.Read())
				{
					throw new JsonSerializationException("Save data has additional content after the envelope");
				}
			}

			if (!version.HasValue || !hasData)
			{
				throw new JsonSerializationException(
					$"Save data must be an object with an integer '{VersionProperty}' and '{DataProperty}'");
			}

			return version.Value;
		}

		/// <summary>
		/// 마이그레이션용으로 세이브를 토큰으로 읽습니다. 날짜 형태 문자열은 문자열 그대로 둡니다.
		/// 실수는 먼저 <see cref="double"/> 로 읽은 뒤, 원문 리터럴이 <see cref="decimal"/> 범위와 정밀도 안이면 토큰마다 <see cref="decimal"/> 로 바꿉니다.
		/// 그래서 큰 <see cref="double"/>(decimal 범위 밖)과 정밀한 <see cref="decimal"/> 이 한 세이브에 함께 있어도 둘 다 보존됩니다.
		/// </summary>
		private static JObject ParseObject(string json)
		{
			JObject root;

			using (var reader = new JsonTextReader(new StringReader(json))
			       {
				       DateParseHandling = DateParseHandling.None,
				       FloatParseHandling = FloatParseHandling.Double
			       })
			{
				root = JObject.Load(reader);
			}

			var floatLiterals = FindFloatLiterals(json);
			var literalIndex = 0;

			// 토큰은 문서 순서로 나오므로 원문의 실수 리터럴과 차례로 맞춘다
			foreach (var token in root.DescendantsAndSelf())
			{
				if (token.Type != JTokenType.Float || !(token is JValue value) || !(value.Value is double parsed))
				{
					continue;
				}

				if (literalIndex >= floatLiterals.Count)
				{
					break;
				}

				var literal = floatLiterals[literalIndex++];

				// 순서가 어긋났거나 decimal 로 바꾸면 값이 달라지는 경우(범위 밖, 자릿수 제한으로 반올림)는 double 그대로 둔다
				if (double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out var literalDouble) &&
				    literalDouble.Equals(parsed) &&
				    decimal.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out var exact) &&
				    IsSameNumber(exact, parsed))
				{
					value.Value = exact;
				}
			}

			return root;
		}

		/// <summary>
		/// 문자열 밖에 있는 실수 리터럴(<c>.</c>, <c>e</c>, <c>E</c> 를 포함한 숫자)의 원문을 문서 순서대로 모읍니다.
		/// </summary>
		private static List<string> FindFloatLiterals(string json)
		{
			var literals = new List<string>();

			for (var i = 0; i < json.Length; i++)
			{
				var c = json[i];

				if (c == '"')
				{
					// 문자열은 이스케이프를 건너뛰며 닫는 따옴표까지 넘긴다
					for (i++; i < json.Length && json[i] != '"'; i++)
					{
						if (json[i] == '\\')
						{
							i++;
						}
					}

					continue;
				}

				if (c != '-' && (c < '0' || c > '9'))
				{
					continue;
				}

				var start = i;
				var isFloat = false;

				while (i + 1 < json.Length && IsNumberChar(json[i + 1]))
				{
					i++;
					isFloat |= json[i] == '.' || json[i] == 'e' || json[i] == 'E';
				}

				if (isFloat)
				{
					literals.Add(json.Substring(start, i - start + 1));
				}
			}

			return literals;
		}

		// decimal 을 문자열로 쓴 값을 double 로 다시 읽어 원래 double 과 같은지 본다. (double)decimal 캐스팅은 마지막 비트가
		// 다를 수 있고 허용 오차 비교는 반올림 손실을 놓치므로, 저장 후 다시 읽을 때와 같은 문자열 경로로 확인한다
		private static bool IsSameNumber(decimal exact, double parsed)
		{
			return double.TryParse(exact.ToString(CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture,
				       out var roundTrip) &&
			       roundTrip.Equals(parsed);
		}

		private static bool IsNumberChar(char c)
		{
			return (c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-';
		}

		private sealed class SaveEnvelope
		{
			[JsonProperty(VersionProperty, Order = 0)]
			public int Version;

			[JsonProperty(DataProperty, Order = 1)]
			public object Data;
		}

		private sealed class DataEnvelope<T>
		{
			[JsonProperty(DataProperty)]
			public T Data;
		}
	}
}
