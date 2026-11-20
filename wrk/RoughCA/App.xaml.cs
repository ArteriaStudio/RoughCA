using Arteria_s.DB.Base;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Arteria_s.App.RoughCA
{
	public class PrepareFlags
	{
		public bool bExistDbParams;		//　データベースとの接続情報が登録されている。
		public bool bExistOrgProfile;	//　
		public bool	bExistAuthority;	//　有効な認証局証明書が存在する。

		public PrepareFlags()
		{
			bExistDbParams   = false;
			bExistOrgProfile = false;
			bExistAuthority  = false;
		}

		//　環境の前提条件の状態を検査
		public void Check(VSQLContext pSQLContext, DbParams m_pDbParams, Authority pAuthority)
		{
			//　データベース接続情報が登録されているか？
			if (m_pDbParams == null)
			{
				bExistDbParams = false;
			}
			else
			{
				bExistDbParams = m_pDbParams.Validate();
			}
			if ((pAuthority == null) || (pAuthority.m_pOrgProfile == null))
			{
				bExistOrgProfile = false;
			}
			else
			{
				bExistOrgProfile = pAuthority.m_pOrgProfile.Validate();
			}

			//　有効な認証局証明書が存在するか？
			if ((pAuthority == null) || (pAuthority.Validate(pSQLContext) == false))
			{
				bExistAuthority = false;
			}
			else
			{
				bExistAuthority = true;
			}

			return;
		}
	}

	/// <summary>
	/// Provides application-specific behavior to supplement the default Application class.
	/// </summary>
	public partial class App : Application
	{
		/// <summary>
		/// Initializes the singleton application object.  This is the first line of authored code
		/// executed, and as such is the logical equivalent of main() or WinMain().
		/// set to .csproj <WindowsAppSDKSelfContained>yes</WindowsAppSDKSelfContained>, if unable to run Unpackaged app.
		/// https://learn.microsoft.com/ja-jp/aspnet/core/host-and-deploy/visual-studio-publish-profiles?view=aspnetcore-8.0
		/// </summary>
		public App()
		{
			//　WinUI3かつUnpackagedのアプリケーションは、動的に表示言語を変更する機能はないと思われる。（2023/11/13：said stack overflow...）
			//Debug.WriteLine(Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride);
			//Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "ja-JP";
			//Windows.ApplicationModel.Resources.Core.ResourceContext.SetGlobalQualifierValue("Language", "de-DE");
			//ApplicationLanguages.PrimaryLanguageOverride
			//Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "ja";

			this.InitializeComponent();
			//this.Exit += App_Exit;

			//　
			Debug.WriteLine(CultureInfo.CurrentUICulture.Name);


			//（これがマスター）
			//　https://learn.microsoft.com/ja-jp/windows/apps/windows-app-sdk/deploy-unpackaged-apps
			//　https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deploy-unpackaged-apps
			//
			// https://learn.microsoft.com/en-us/windows/apps/winui/winui3/localize-winui3-app
			// https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/mrtcore/localize-strings
			// https://learn.microsoft.com/ja-jp/windows/apps/winui/winui3/localize-winui3-app
			// https://learn.microsoft.com/ja-jp/windows/apps/windows-app-sdk/mrtcore/localize-strings
			// https://nicksnettravels.builttoroam.com/mrtcore-unpackaged/
			// （非パッケージ化のWinUI3アプリケーションは、手動でPRIファイルを生成する必要がある様子：2023/11/17）
			// https://tera1707.com/entry/2023/07/31/225012
			// （グローバリゼーション）
			// https://learn.microsoft.com/ja-jp/windows/apps/design/globalizing/globalizing-portal
			//
			// https://learn.microsoft.com/ja-jp/windows/uwp/app-resources/localize-strings-ui-manifest#localize-the-string-resources
			// https://learn.microsoft.com/ja-jp/windows/uwp/app-resources/makepri-exe-configuration
			//（実装サンプル）
			// https://xamlbrewer.wordpress.com/category/windows-app-sdk/
		}

		/// <summary>
		/// Invoked when the application is launched.
		/// </summary>
		/// <param name="args">Details about the launch request and process.</param>
		protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
		{
			string pFilepath = "";
			var pArgs = Environment.GetCommandLineArgs();

			if (pArgs.Length > 1)
			{
				pFilepath = pArgs[1];
			}
			m_pProfile = new Profile(pFilepath);
			m_pDbParams = new DbParams();
			m_pProfile.Load(ref m_pDbParams);
			m_pPrepareFlags = new PrepareFlags();

			ConnectDatabase(m_pDbParams, out _);

			m_pWindow = new MainWindow();
			m_pWindow.Title = "RoughCA [" + m_pDbParams.ProfileName + "]";
			m_pWindow.Activate();
		}

		//　指定したパラメータセットでデータベースへ再接続し、認証局情報を再読み込み
		public bool ConnectDatabase(DbParams pDbParams, out string errorMessage)
		{
			errorMessage = null;

			if (m_pSQLContext != null)
			{
				try
				{
					m_pSQLContext.Close();
				}
				catch (Exception ex)
				{
					Debug.WriteLine("Close SQLContext error: " + ex);
				}
				m_pSQLContext = null;
			}

			m_pDbParams = pDbParams;
			m_pCertsStock = Authority.Instance;
			m_pCertsStock.m_pAuthorityItem = null;
			m_pCertsStock.m_pOrgProfile = new OrgProfile();

			bool connected = false;
			if (m_pDbParams != null && m_pDbParams.Validate() == true)
			{
				try
				{
					if (m_pDbParams.DriverName.Equals("Postgres") == true)
					{
						m_pSQLContext = new SQLContext(
							m_pDbParams.HostName,
							m_pDbParams.DatabaseName,
							m_pDbParams.SchemaName,
							m_pDbParams.ClientKey,
							m_pDbParams.ClientCrt,
							m_pDbParams.TrustCrt);
					}
					else if (m_pDbParams.DriverName.Equals("SQLite") == true)
					{
						m_pSQLContext = new SQLiteContext(
							m_pDbParams.HostName,
							m_pDbParams.DatabaseName,
							m_pDbParams.SchemaName,
							m_pDbParams.ClientKey,
							m_pDbParams.ClientCrt,
							m_pDbParams.TrustCrt,
							m_pDbParams.SqliteFilePath);
					}
					else
					{
						m_pSQLContext = null;
					}

					if (m_pSQLContext != null)
					{
						var identityKey = string.IsNullOrWhiteSpace(m_pDbParams.DatabaseName)
							? m_pDbParams.ProfileName
							: m_pDbParams.DatabaseName;
						m_pCertsStock.Load(m_pSQLContext, identityKey, m_pDbParams.uInstance);
						connected = true;
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine("ConnectDatabase error: " + ex);
					errorMessage = ex.Message;
					m_pSQLContext = null;
				}
			}

			if (m_pPrepareFlags == null)
			{
				m_pPrepareFlags = new PrepareFlags();
			}
			m_pPrepareFlags.Check(m_pSQLContext, m_pDbParams, m_pCertsStock);

			if (m_pWindow != null && m_pDbParams != null)
			{
				m_pWindow.Title = "RoughCA [" + m_pDbParams.ProfileName + "]";
			}

			return connected;
		}

		//　プロファイルを切り替え
		public bool SwitchProfile(string profileName, out string errorMessage)
		{
			var target = m_pProfile.SelectProfile(profileName);
			if (target == null)
			{
				errorMessage = $"プロファイル '{profileName}' が見つかりません。";
				return false;
			}
			return ConnectDatabase(target, out errorMessage);
		}

		//　新規プロファイルを追加して切り替え
		public bool AddAndSwitchProfile(DbParams newProfile, out string errorMessage)
		{
			if (!m_pProfile.AddProfile(newProfile, activate: true))
			{
				errorMessage = "同じ名前のプロファイルが既に存在するか、プロファイル名が不正です。";
				return false;
			}
			return ConnectDatabase(newProfile, out errorMessage);
		}

		//　指定したプロファイルを削除して別のプロファイルへ切り替え
		public bool DeleteProfile(string profileName, out string errorMessage)
		{
			if (!m_pProfile.DeleteProfile(profileName, out var nextProfile))
			{
				errorMessage = "最後のプロファイルは削除できません。";
				return false;
			}
			return ConnectDatabase(nextProfile, out errorMessage);
		}

		//　DB接続設定を保存して再接続
		public bool SaveDbParamsAndReconnect(DbParams pDbParams, out string errorMessage)
		{
			m_pProfile.Save(pDbParams);
			return ConnectDatabase(pDbParams, out errorMessage);
		}

		//　編集解除時に組織プロファイルを保存し、未発行のルート/発行認証局の鍵と自己証明書を作成する
		public bool CommitOrgProfileAndEnsureAuthority(
			DbParams pDbParams,
			OrgProfile pOrgProfile,
			out bool createdRoot,
			out bool createdIssue,
			out string errorMessage)
		{
			createdRoot = false;
			createdIssue = false;
			errorMessage = null;

			if (pDbParams == null || pOrgProfile == null)
			{
				errorMessage = "接続設定または認証局情報が未初期化です。";
				return false;
			}

			//　DB接続がまだ確立していなければ接続を試行
			if (m_pSQLContext == null)
			{
				if (!ConnectDatabase(pDbParams, out errorMessage))
				{
					return false;
				}
			}

			try
			{
				m_pProfile.Save(pDbParams);
				pOrgProfile.OrgKey = pDbParams.uInstance;
				m_pCertsStock = Authority.Instance;
				m_pCertsStock.m_pOrgProfile = pOrgProfile;

				m_pSQLContext.SaveOrgProfile(pOrgProfile);

				var identityKey = string.IsNullOrWhiteSpace(pDbParams.DatabaseName)
					? pDbParams.ProfileName
					: pDbParams.DatabaseName;

				var ensured = m_pCertsStock.EnsureAuthorityCertificates(
					m_pSQLContext,
					identityKey,
					pDbParams.uInstance,
					out createdRoot,
					out createdIssue);

				m_pPrepareFlags?.Check(m_pSQLContext, m_pDbParams, m_pCertsStock);

				if (!ensured)
				{
					errorMessage = "認証局の鍵または証明書の作成に失敗しました。";
					return false;
				}

				return true;
			}
			catch (AppException ex)
			{
				Debug.WriteLine("CommitOrgProfileAndEnsureAuthority AppException: " + ex);
				errorMessage = ex.GetText();
				return false;
			}
			catch (Exception ex)
			{
				Debug.WriteLine("CommitOrgProfileAndEnsureAuthority Exception: " + ex);
				errorMessage = ex.Message;
				return false;
			}
		}

		//　組織プロファイルを保存
		public void SaveOrgProfile(DbParams pDbParams)
		{
			if (m_pSQLContext != null)
			{
				m_pSQLContext.SaveOrgProfile(m_pCertsStock.m_pOrgProfile);
			}

			m_pProfile.Save(pDbParams);

			//　ルート証明書を再読み込み
			if (m_pSQLContext != null)
			{
				m_pCertsStock = Authority.Instance;
				var identityKey = string.IsNullOrWhiteSpace(m_pDbParams.DatabaseName)
					? m_pDbParams.ProfileName
					: m_pDbParams.DatabaseName;
				m_pCertsStock.Load(m_pSQLContext, identityKey, m_pDbParams.uInstance);
			}
			m_pPrepareFlags?.Check(m_pSQLContext, m_pDbParams, m_pCertsStock);
		}

		public VSQLContext	GetSQLContext()
		{
			return (m_pSQLContext);
		}

		protected VSQLContext	m_pSQLContext;

		public Window m_pWindow;
		public Profile m_pProfile;
		public DbParams m_pDbParams;
		public PrepareFlags m_pPrepareFlags;	//　前提条件検査結果（）
		public Authority m_pCertsStock;
	}
}
