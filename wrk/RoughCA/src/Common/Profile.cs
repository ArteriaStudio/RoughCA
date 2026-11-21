using Arteria_s.DB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arteria_s.App.RoughCA
{
	public class DbParams : Data
	{
		private static readonly SortedDictionary<string, int> DriverNameToIndexMap = new SortedDictionary<string, int>
		{
			["SQLite"] = 0,
			["Postgres"] = 1,
		};
		private static readonly SortedDictionary<int, string> DriverIndexToNameMap = new SortedDictionary<int, string>
		{
			[0] = "SQLite",
			[1] = "Postgres",
		};

		private static readonly SortedDictionary<string, int> ExportFormatToIndexMap = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase)
		{
			["PEM"] = 0,
			["DER"] = 1,
		};
		private static readonly SortedDictionary<int, string> ExportFormatIndexToNameMap = new SortedDictionary<int, string>
		{
			[0] = "PEM",
			[1] = "DER",
		};

		public DbParams()
		{
			ProfileName    = "Default";
			UserIdentity   = 0;
			HostName       = "";
			DatabaseName   = "RoughCA";
			SchemaName     = "";
			ClientKey      = "";
			ClientCrt      = "";
			TrustCrt       = "";
			SqliteFilePath = "";
			DriverName     = "SQLite";
			ExportFormat   = "DER";
			ExportFolder   = "";
			uInstance      = 0;
		}

		[JsonPropertyName("ProfileName")]
		public string ProfileName { get; set; }
		[JsonPropertyName("UserIdentity")]
		public int UserIdentity { get; set; }
		[JsonPropertyName("HostName")]
		public string HostName { get; set; }
		[JsonPropertyName("DatabaseName")]
		public string DatabaseName { get; set; }
		[JsonPropertyName("SchemaName")]
		public string SchemaName { get; set; }
		[JsonPropertyName("ClientKey")]
		public string ClientKey { get; set; }
		[JsonPropertyName("ClientCrt")]
		public string ClientCrt { get; set; }
		[JsonPropertyName("TrustCrt")]
		public string TrustCrt { get; set; }
		[JsonPropertyName("SqliteFilePath")]
		public string SqliteFilePath { get; set; }

		private string _DriverName = "SQLite";
		[JsonPropertyName("DriverName")]
		public string DriverName
		{
			get => _DriverName;
			set
			{
				_DriverName = value ?? "";
				if (DriverNameToIndexMap.TryGetValue(_DriverName, out var idx))
				{
					_DriverIndex = idx;
				}
			}
		}

		private int _DriverIndex = 0;
		[JsonIgnore]
		public int DriverIndex
		{
			get => _DriverIndex;
			set
			{
				if (value < 0 || !DriverIndexToNameMap.ContainsKey(value)) value = 0;
				_DriverIndex = value;
				_DriverName = DriverIndexToNameMap[value];
			}
		}

		[JsonPropertyName("uInstance")]
		public uint uInstance { get; set; }

		private string _ExportFormat = "DER";
		[JsonPropertyName("ExportFormat")]
		public string ExportFormat
		{
			get => _ExportFormat;
			set
			{
				var normalized = string.IsNullOrWhiteSpace(value) ? "DER" : value.Trim().ToUpperInvariant();
				if (ExportFormatToIndexMap.TryGetValue(normalized, out var idx))
				{
					_ExportFormat = ExportFormatIndexToNameMap[idx];
					_ExportFormatIndex = idx;
				}
				else
				{
					_ExportFormat = "DER";
					_ExportFormatIndex = 1;
				}
			}
		}

		private int _ExportFormatIndex = 1;
		[JsonIgnore]
		public int ExportFormatIndex
		{
			get => _ExportFormatIndex;
			set
			{
				if (!ExportFormatIndexToNameMap.ContainsKey(value)) value = 1;
				_ExportFormatIndex = value;
				_ExportFormat = ExportFormatIndexToNameMap[value];
			}
		}

		[JsonPropertyName("ExportFolder")]
		public string ExportFolder { get; set; } = "";

		public override bool Validate()
		{
			if (IsNull(DriverName) == true)
			{
				//　データベースドライバを選択していない。
				return (false);
			}
			if (DriverName.Equals("Postgres") == true)
			{
				if (IsNull(HostName) == true)
				{
					return (false);
				}
				if (IsNull(DatabaseName) == true)
				{
					return (false);
				}
				if (IsNull(SchemaName) == true)
				{
					return (false);
				}
				if (IsNull(ClientKey) == true)
				{
					return (false);
				}
				if (IsNull(ClientCrt) == true)
				{
					return (false);
				}
				if (IsNull(TrustCrt) == true)
				{
					return (false);
				}
			}
			else if (DriverName.Equals("SQLite") == true)
			{
				if (IsNull(DatabaseName) == true && IsNull(SqliteFilePath) == true)
				{
					return (false);
				}
			}
			else
			{
				return (false);
			}

			return (true);
		}
	}

	public class ProfileStore
	{
		[JsonPropertyName("ActiveProfileName")]
		public string ActiveProfileName { get; set; } = "Default";

		[JsonPropertyName("Profiles")]
		public List<DbParams> Profiles { get; set; } = new List<DbParams>();
	}

	public class Profile : ProfileBase
	{
		private static readonly string m_pCompanyName = "Arteria";
		private static readonly string m_pAppName = "RoughCA";
		private const string pProfileFilename = "RoughCA.json";

		public ProfileStore Store { get; private set; } = new ProfileStore();
		public IReadOnlyList<DbParams> Profiles => Store.Profiles;
		public string ActiveProfileName => Store.ActiveProfileName;
		public string ProfileFolderPath => m_pProfileFolderPath;

		public Profile(string pArgument) : base(m_pCompanyName, m_pAppName)
		{
		}

		public string GetDefaultSqlitePath(string databaseName)
		{
			var name = string.IsNullOrWhiteSpace(databaseName) ? m_pAppName : databaseName.Trim();
			return Path.Combine(m_pProfileFolderPath, name + ".db");
		}

		public string ResolveSqlitePath(DbParams pDbParams)
		{
			if (pDbParams == null)
			{
				return GetDefaultSqlitePath(m_pAppName);
			}
			if (!string.IsNullOrWhiteSpace(pDbParams.SqliteFilePath))
			{
				return pDbParams.SqliteFilePath.Trim();
			}
			return GetDefaultSqlitePath(pDbParams.DatabaseName);
		}

		public string GetDefaultExportFolder()
		{
			var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			if (string.IsNullOrWhiteSpace(userProfile))
			{
				userProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? "";
			}
			return Path.Combine(userProfile, "Downloads");
		}

		public string ResolveExportFolder(DbParams pDbParams)
		{
			if (pDbParams != null && !string.IsNullOrWhiteSpace(pDbParams.ExportFolder))
			{
				return pDbParams.ExportFolder.Trim();
			}
			return GetDefaultExportFolder();
		}

		public bool IsPemExport(DbParams pDbParams)
		{
			return string.Equals(pDbParams?.ExportFormat, "PEM", StringComparison.OrdinalIgnoreCase);
		}

		public static DbParams CreateDefaultDbParams(string profileName = "Default", string databaseName = null)
		{
			var pAppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			var pFolderPath = Path.Combine(pAppDataFolder, "postgresql");

			return new DbParams
			{
				ProfileName    = string.IsNullOrWhiteSpace(profileName) ? "Default" : profileName.Trim(),
				UserIdentity   = 0,
				HostName       = "",
				DatabaseName   = string.IsNullOrWhiteSpace(databaseName) ? m_pAppName : databaseName.Trim(),
				SchemaName     = "aploper",
				ClientKey      = Path.Combine(pFolderPath, "postgresql.key"),
				ClientCrt      = Path.Combine(pFolderPath, "postgresql.crt"),
				TrustCrt       = Path.Combine(pFolderPath, "root.crt"),
				SqliteFilePath = "",
				DriverName     = "SQLite",
				uInstance      = 0,
			};
		}

		public bool Load(ref DbParams m_pDbParams)
		{
			Store = LoadProfileStore(m_pProfileFolderPath);
			if (Store == null || Store.Profiles == null || Store.Profiles.Count == 0)
			{
				var defaultParams = CreateDefaultDbParams("Default", m_pAppName);
				Store = new ProfileStore
				{
					ActiveProfileName = defaultParams.ProfileName,
					Profiles = new List<DbParams> { defaultParams }
				};
				SaveProfileStore(m_pProfileFolderPath, Store);
			}

			var active = FindProfile(Store.ActiveProfileName) ?? Store.Profiles[0];
			Store.ActiveProfileName = active.ProfileName;
			m_pDbParams = active;
			return (true);
		}

		public DbParams FindProfile(string profileName)
		{
			if (string.IsNullOrWhiteSpace(profileName) || Store?.Profiles == null)
			{
				return null;
			}
			return Store.Profiles.FirstOrDefault(p => string.Equals(p.ProfileName, profileName, StringComparison.OrdinalIgnoreCase));
		}

		public bool Save(DbParams pDbParams)
		{
			if (pDbParams == null)
			{
				return false;
			}
			if (string.IsNullOrWhiteSpace(pDbParams.ProfileName))
			{
				pDbParams.ProfileName = "Default";
			}

			var index = Store.Profiles.FindIndex(p => ReferenceEquals(p, pDbParams));
			if (index < 0)
			{
				index = Store.Profiles.FindIndex(p => string.Equals(p.ProfileName, pDbParams.ProfileName, StringComparison.OrdinalIgnoreCase));
			}

			if (index >= 0)
			{
				Store.Profiles[index] = pDbParams;
			}
			else
			{
				Store.Profiles.Add(pDbParams);
			}

			Store.ActiveProfileName = pDbParams.ProfileName;
			return SaveProfileStore(m_pProfileFolderPath, Store);
		}

		public DbParams SelectProfile(string profileName)
		{
			var target = FindProfile(profileName);
			if (target == null)
			{
				return null;
			}
			Store.ActiveProfileName = target.ProfileName;
			SaveProfileStore(m_pProfileFolderPath, Store);
			return target;
		}

		public bool AddProfile(DbParams newProfile, bool activate = true)
		{
			if (newProfile == null || string.IsNullOrWhiteSpace(newProfile.ProfileName))
			{
				return false;
			}
			newProfile.ProfileName = newProfile.ProfileName.Trim();
			if (FindProfile(newProfile.ProfileName) != null)
			{
				return false;
			}

			Store.Profiles.Add(newProfile);
			if (activate)
			{
				Store.ActiveProfileName = newProfile.ProfileName;
			}
			return SaveProfileStore(m_pProfileFolderPath, Store);
		}

		public bool DeleteProfile(string profileName, out DbParams newActiveProfile)
		{
			newActiveProfile = null;
			if (Store.Profiles.Count <= 1)
			{
				return false;
			}
			var target = FindProfile(profileName);
			if (target == null)
			{
				return false;
			}

			Store.Profiles.Remove(target);
			if (string.Equals(Store.ActiveProfileName, target.ProfileName, StringComparison.OrdinalIgnoreCase))
			{
				Store.ActiveProfileName = Store.Profiles[0].ProfileName;
			}
			newActiveProfile = FindProfile(Store.ActiveProfileName) ?? Store.Profiles[0];
			return SaveProfileStore(m_pProfileFolderPath, Store);
		}

		//　ProfileBase の抽象メソッド（現在は JSON ストアに統一）
		protected override bool DoUpgradeLayout(Microsoft.Data.Sqlite.SqliteCommand pCommand)
		{
			return (true);
		}

		protected ProfileStore LoadProfileStore(string pProfileFolder)
		{
			var pFilepath = Path.Combine(pProfileFolder, pProfileFilename);
			if (File.Exists(pFilepath) == false)
			{
				return null;
			}

			var pJSON = File.ReadAllText(pFilepath);
			if (string.IsNullOrWhiteSpace(pJSON))
			{
				return null;
			}

			try
			{
				using var doc = JsonDocument.Parse(pJSON);
				if (doc.RootElement.ValueKind == JsonValueKind.Object &&
					doc.RootElement.TryGetProperty("Profiles", out _))
				{
					var store = JsonSerializer.Deserialize<ProfileStore>(pJSON);
					if (store?.Profiles != null && store.Profiles.Count > 0)
					{
						foreach (var p in store.Profiles)
						{
							if (string.IsNullOrWhiteSpace(p.ProfileName))
							{
								p.ProfileName = string.IsNullOrWhiteSpace(p.DatabaseName) ? "Default" : p.DatabaseName;
							}
						}
						return store;
					}
				}

				//　旧形式（単一 DbParams JSON）からの自動マイグレーション
				var legacy = JsonSerializer.Deserialize<DbParams>(pJSON);
				if (legacy != null)
				{
					if (string.IsNullOrWhiteSpace(legacy.ProfileName))
					{
						legacy.ProfileName = "Default";
					}
					if (string.IsNullOrWhiteSpace(legacy.DatabaseName))
					{
						legacy.DatabaseName = m_pAppName;
					}
					var migrated = new ProfileStore
					{
						ActiveProfileName = legacy.ProfileName,
						Profiles = new List<DbParams> { legacy }
					};
					SaveProfileStore(pProfileFolder, migrated);
					return migrated;
				}
			}
			catch (JsonException ex)
			{
				System.Diagnostics.Debug.WriteLine("Profile JSON parse error: " + ex);
			}

			return null;
		}

		protected bool SaveProfileStore(string pProfileFolder, ProfileStore pStore)
		{
			var pFilepath = Path.Combine(pProfileFolder, pProfileFilename);
			var pOptions = new JsonSerializerOptions { WriteIndented = true };
			var pJSON = JsonSerializer.Serialize(pStore, pOptions);
			File.WriteAllText(pFilepath, pJSON);
			return (true);
		}
	}
}
