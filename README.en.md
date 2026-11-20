# RoughCA

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%2F11%20(x64)-0078D6.svg)](#requirements--tech-stack)
[![UI: WinUI 3](https://img.shields.io/badge/UI-WinUI%203-512BD4.svg)](#requirements--tech-stack)

[日本語](README.md) | **English** | [简体中文](README.zh-CN.md)

**RoughCA** (Arteria Studio RoughCA) is a desktop application for managing a Private Certificate Authority (CA) on Windows.  
Built with WinUI 3 (Windows App SDK) and .NET, it provides a clean GUI to bootstrap Root and Issuing CAs, issue, renew, revoke, and export X.509 certificates, sign CSRs, and generate CRLs.

---

## Features

- **Automated CA Bootstrapping & Multi-Profile Management**
  - Automatically generates key pairs and certificates for both a self-signed Root CA and an Issuing (Intermediate) CA based on your organization profile (`O`, `OU`, `ST`, `L`, `C`, etc.).
  - Supports multiple CA profiles with quick switching via the profile badge in the header.
- **X.509 Certificate Issuance & Lifecycle Management**
  - **Server Certificates** (SAN: DNS Name / `serverAuth`)
  - **Client Certificates** (Email protection, SAN: Email / `emailProtection`)
  - **Remote Desktop Listener Certificates** (FQDN & IPv4 support)
  - **CA Certificates**
  - View issued certificates, renew validity periods, revoke certificates, and export certificates (`.crt`) and private keys (`.key`).
- **Primitive PKI Operations (CSR, Signing & CRL)**
  - Create and export Certificate Signing Requests (`.csr`)
  - Sign external CSRs (for Server or CA certificates)
  - Import signed CA certificates
  - Generate and export Certificate Revocation Lists (`.crl`)
- **Dual Data Store Support (SQLite / PostgreSQL)**
  - **SQLite**: Zero-server local storage for immediate standalone use.
  - **PostgreSQL**: Supports secure remote connections using mutual TLS (`SslMode=VerifyFull`) with client certificates.
- **Modern Windows UI**
  - Features Mica Backdrop, Light/Dark themes, a bottom-docked message log viewer, and an edit-lock toggle on the settings page to prevent accidental modifications.
  - Includes localization resources for Japanese (`ja-JP`) and English (`en-US`).

---

## Cryptographic Specifications

| Item | Specification |
| :--- | :--- |
| **Key Algorithm** | ECDSA P-384 (384-bit) — chosen for compatibility with Chromium-based browsers |
| **Signature Hash Algorithm** | SHA-512 |
| **Database Storage Format** | PEM (certificates and private keys) |
| **Export Extensions** | `.crt` (Certificate), `.key` (Private Key), `.csr` (CSR), `.crl` (CRL) |
| **Default Validity Periods** | Root CA: 10 years / Issuing CA: 5 years / Server Certificate (from CSR): 1 year |

---

## Requirements & Tech Stack

- **OS**: Windows 10 version 1809 (Build 17763) or later / Windows 11 (x64)
- **Runtime / SDK**: .NET (`net11.0-windows10.0.22621.0`), Windows App SDK 2.7.0 (WinUI 3)
- **Language**: C# / XAML
- **Data Stores**:
  - SQLite (`Microsoft.Data.Sqlite`)
  - PostgreSQL (`Npgsql`)

---

## Build & Run

### Using Visual Studio

1. Clone the repository:
   ```bash
   git clone <repository-url>
   cd RoughCA
   ```
2. Open `RoughCA.sln` in Visual Studio.
3. Select the `x64` platform, then build and run.

### Using .NET CLI

```powershell
dotnet restore RoughCA.sln
dotnet build RoughCA.sln -c Release -p:Platform=x64
```

---

## Quick Start

### 1. Configure the Data Store (SQLite)

1. Launch the application and open **Settings** from the navigation menu.
2. Unlock the settings page using the lock toggle in the upper-right corner to enter edit mode.
3. In the **Connection** tab, select `SQLite`, then enter a Profile Name and Database Name.
   - If the SQLite file path is left blank, RoughCA automatically creates and uses `%APPDATA%\Arteria\RoughCA\{DatabaseName}.db`.
4. Click **Save and Reconnect**.

### 2. Set Up Organization Profile & CA

1. Switch to the **Authority Info** tab in Settings.
2. Fill in the Root CA Name (`trustName`), Issuing CA Name (`issueName`), and Organization Profile fields (`O`, `OU`, `ST`, `L`, `C`, Publish Server Name).
3. Once configured, RoughCA automatically creates any missing Root/Issuing CA certificates so you can immediately begin issuing certificates.

### PostgreSQL Connection Parameters

When using PostgreSQL as the backend, configure the following parameters for mutual TLS (mTLS) authentication:

| Parameter | Description |
| :--- | :--- |
| **HostName** | Hostname (FQDN) of the PostgreSQL server |
| **DatabaseName** | Target database name |
| **SchemaName** | Database username used for the connection |
| **ClientSecretKey** | Client private key file for mTLS authentication |
| **ClientCertificate** | Client certificate file for mTLS authentication |
| **RootCertificate** | Trusted Root CA certificate file used to verify the PostgreSQL server |

> Reference table definitions are available in [`Scripts/autoca.sql`](Scripts/autoca.sql).

---

## Project Structure

```text
RoughCA/
├── RoughCA.sln                    # Visual Studio solution
├── Scripts/
│   └── autoca.sql                 # Database table definition script
└── wrk/RoughCA/
    ├── RoughCA.csproj             # WinUI 3 project configuration
    ├── App.xaml(.cs)              # Application entry point
    ├── MainWindow.xaml(.cs)       # Main window, navigation & profile switcher
    ├── Strings/                   # Localization resources (ja-JP / en-US)
    ├── Assets/                    # App icons and visual assets
    └── src/
        ├── CA/                    # CA core logic (Authority, Certificate, SignRequest, etc.)
        ├── Common/                # Profile management, helpers & custom exceptions
        ├── DB/                    # Data access layer (SQLiteContext, SQLContext, VSQLContext)
        └── Page/                  # UI pages (History, Issue, CSR, Signing, Settings, Version)
```

---

## License

This project is licensed under the [MIT License](LICENSE).  
2024-2026 ArteriaStudio
