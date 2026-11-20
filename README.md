# RoughCA

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%2F11%20(x64)-0078D6.svg)](#動作環境技術スタック)
[![UI: WinUI 3](https://img.shields.io/badge/UI-WinUI%203-512BD4.svg)](#動作環境技術スタック)

**日本語** | [English](README.en.md) | [简体中文](README.zh-CN.md)

**RoughCA**（Arteria Studio RoughCA）は、Windows 向けのプライベート認証局（Private Certificate Authority）管理デスクトップアプリケーションです。  
WinUI 3（Windows App SDK）と .NET で構築されており、ルート認証局・発行用認証局の構築から、各種 X.509 証明書の発行・更新・失効・エクスポート、CSR への署名、CRL の生成までを直感的な GUI で一元管理できます。

---

## 主な機能

- **認証局の自動構築とマルチプロファイル管理**
  - 組織プロファイル（`O` / `OU` / `ST` / `L` / `C` など）に基づき、ルート CA（自己署名）および発行用 CA（中間 CA）の鍵ペアと証明書を自動生成します。
  - 複数の認証局プロファイルを登録でき、ウィンドウ上部のプロファイルバッジからワンクリックで切り替えられます。
- **多様な X.509 証明書の発行・ライフサイクル管理**
  - **サーバー証明書**（SAN: DNS 名 / `serverAuth`）
  - **クライアント証明書**（メール保護用途、SAN: Email / `emailProtection`）
  - **リモートデスクトップリスナー証明書**（FQDN・IPv4 アドレス対応）
  - **認証局（CA）証明書**
  - 発行済み証明書の一覧表示、有効期限の更新、失効（Revoke）、証明書（`.crt`）・秘密鍵（`.key`）のファイル出力に対応しています。
- **プリミティブ操作（CSR・署名・CRL）**
  - 署名要求（CSR: `.csr`）の生成・エクスポート
  - 外部 CSR（サーバー証明書 / 認証局証明書）への署名実行
  - 署名済み CA 証明書のインポート
  - 証明書失効リスト（CRL: `.crl`）の生成とエクスポート
- **デュアルデータストア対応（SQLite / PostgreSQL）**
  - **SQLite**: サーバー構築不要で、ローカルファイルだけで即座に運用を開始できます。
  - **PostgreSQL**: クライアント証明書認証（mTLS / `SslMode=VerifyFull`）による安全なデータベース接続に対応します。
- **モダンな Windows UI**
  - Mica Backdrop、ライト / ダークテーマ、下部ドック型のメッセージログ表示を備えています。
  - 設定画面には編集ロック機構を備え、誤操作による設定変更を防止します。
  - 日本語（`ja-JP`）・英語（`en-US`）の表示リソースを同梱しています。

---

## 暗号・証明書仕様

| 項目 | 仕様 |
| :--- | :--- |
| **公開鍵暗号方式** | ECDSA（楕円曲線暗号）P-384（384-bit）※ Chromium 系ブラウザ互換性を考慮 |
| **署名ハッシュアルゴリズム** | SHA-512 |
| **DB 保管形式** | PEM 形式（証明書・秘密鍵） |
| **出力ファイル拡張子** | `.crt`（証明書）、`.key`（秘密鍵）、`.csr`（署名要求）、`.crl`（失効リスト） |
| **既定の有効期間** | ルート CA: 10 年 / 発行用 CA: 5 年 / サーバー証明書（CSR 署名時）: 1 年 |

---

## 動作環境・技術スタック

- **OS**: Windows 10 バージョン 1809（Build 17763）以降 / Windows 11（x64）
- **ランタイム / SDK**: .NET（`net11.0-windows10.0.22621.0`）、Windows App SDK 2.7.0（WinUI 3）
- **言語**: C# / XAML
- **データストア**:
  - SQLite（`Microsoft.Data.Sqlite`）
  - PostgreSQL（`Npgsql`）

---

## ビルドと実行

### Visual Studio を使用する場合

1. リポジトリをクローンします。
   ```bash
   git clone <repository-url>
   cd RoughCA
   ```
2. `RoughCA.sln` を Visual Studio で開きます。
3. プラットフォームに `x64` を選択し、ビルド・実行します。

### .NET CLI を使用する場合

```powershell
dotnet restore RoughCA.sln
dotnet build RoughCA.sln -c Release -p:Platform=x64
```

---

## クイックスタート

### 1. データストアの設定（SQLite で始める場合）

1. アプリを起動し、左メニューから **設定** を開きます。
2. 右上のロックボタンを解除して編集モードにします。
3. **接続設定** タブでドライバーに `SQLite` を選択し、プロファイル名とデータベース名を入力します。
   - SQLite ファイルパスを空欄にした場合、既定で `%APPDATA%\Arteria\RoughCA\{DatabaseName}.db` が自動的に作成・使用されます。
4. **保存して再接続** をクリックします。

### 2. 組織プロファイルと認証局のセットアップ

1. 設定画面の **認証局情報** タブを開きます。
2. ルート認証局名（`trustName`）、発行用認証局名（`issueName`）、および組織情報（組織名 `O`、部署名 `OU`、都道府県 `ST`、市町村 `L`、国名 `C`、公開サーバ名）を入力します。
3. 必要な情報が揃うと、未発行のルート CA・発行用 CA 証明書が自動的に生成され、証明書の発行が可能になります。

### PostgreSQL を使用する場合の接続パラメータ

PostgreSQL をバックエンドに選択する場合、mTLS（相互 TLS 認証）での接続に以下のパラメータを設定します。

| パラメータ | 説明 |
| :--- | :--- |
| **サーバのホスト名（HostName）** | PostgreSQL サーバーのホスト名（FQDN） |
| **データベース名（DatabaseName）** | 接続先のデータベース名 |
| **スキーマ名 / ユーザー名（SchemaName）** | データベース接続に使用するユーザー名 |
| **クライアント秘密鍵（ClientSecretKey）** | mTLS 接続時に使用するクライアント秘密鍵ファイル |
| **クライアント証明書（ClientCertificate）** | mTLS 接続時に使用するクライアント証明書ファイル |
| **ルート証明書（RootCertificate）** | PostgreSQL サーバーの証明書を検証する信頼ルート証明書ファイル |

> データベーススキーマの参考定義は [`Scripts/autoca.sql`](Scripts/autoca.sql) にあります。

---

## プロジェクト構成

```text
RoughCA/
├── RoughCA.sln                    # Visual Studio ソリューション
├── Scripts/
│   └── autoca.sql                 # DB テーブル定義スクリプト
└── wrk/RoughCA/
    ├── RoughCA.csproj             # WinUI 3 プロジェクト設定
    ├── App.xaml(.cs)              # アプリケーションエントリポイント
    ├── MainWindow.xaml(.cs)       # メインウィンドウ・ナビゲーション・プロファイル切替
    ├── Strings/                   # 多言語リソース (ja-JP / en-US)
    ├── Assets/                    # アプリアイコン・アセット
    └── src/
        ├── CA/                    # 認証局ロジック (Authority, Certificate, SignRequest 等)
        ├── Common/                # プロファイル管理・ヘルパー・例外定義
        ├── DB/                    # データアクセス層 (SQLiteContext, SQLContext, VSQLContext)
        └── Page/                  # 各機能ページ (発行履歴・作成・CSR・署名・設定・バージョン)
```

---

## ライセンス

本プロジェクトは [MIT License](LICENSE) の下で公開されています。  
2024-2026 Arteria Studio
