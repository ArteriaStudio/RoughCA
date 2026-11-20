# RoughCA

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%2F11%20(x64)-0078D6.svg)](#运行环境与技术栈)
[![UI: WinUI 3](https://img.shields.io/badge/UI-WinUI%203-512BD4.svg)](#运行环境与技术栈)

[日本語](README.md) | [English](README.en.md) | **简体中文**

**RoughCA**（Arteria Studio RoughCA）是一款面向 Windows 的私有证书颁发机构（Private Certificate Authority）桌面管理应用。  
基于 WinUI 3（Windows App SDK）与 .NET 构建，提供直观的图形界面，帮助您一站式完成根 CA / 颁发 CA 的搭建、各类 X.509 证书的签发、续期、吊销与导出，以及 CSR 签名和 CRL 生成。

---

## 主要功能

- **CA 自动初始化与多配置文件管理**
  - 根据组织信息（`O` / `OU` / `ST` / `L` / `C` 等）自动生成自签名根 CA 和颁发用 CA（中间 CA）的密钥对及证书。
  - 支持管理多个 CA 配置文件，可通过窗口顶部的配置文件徽章快速切换。
- **多种 X.509 证书签发与生命周期管理**
  - **服务器证书**（SAN: DNS 名称 / `serverAuth`）
  - **客户端证书**（邮件保护用途，SAN: Email / `emailProtection`）
  - **远程桌面侦听器证书**（支持 FQDN 与 IPv4 地址）
  - **CA 证书**
  - 支持已签发证书列表查看、有效期续期、吊销（Revoke）以及证书（`.crt`）与私钥（`.key`）文件导出。
- **基础 PKI 操作（CSR / 签名 / CRL）**
  - 生成并导出证书签名请求（CSR: `.csr`）
  - 对外部 CSR（服务器证书 / CA 证书）执行签名
  - 导入已签名的 CA 证书
  - 生成并导出证书吊销列表（CRL: `.crl`）
- **双数据存储支持（SQLite / PostgreSQL）**
  - **SQLite**：无需部署数据库服务器，仅通过本地文件即可开箱即用。
  - **PostgreSQL**：支持基于客户端证书的双向 TLS 认证（mTLS / `SslMode=VerifyFull`），保障远程数据库连接安全。
- **现代 Windows 界面**
  - 采用 Mica 材质背景、支持浅色 / 深色主题及底部停靠式消息日志面板。
  - 设置界面内置编辑锁定机制，有效防止误操作修改配置。
  - 内置日语（`ja-JP`）与英语（`en-US`）多语言资源。

---

## 加密与证书规格

| 项目 | 规格 |
| :--- | :--- |
| **公钥加密算法** | ECDSA（椭圆曲线加密）P-384（384 位）※ 兼顾 Chromium 内核浏览器兼容性 |
| **签名哈希算法** | SHA-512 |
| **数据库存储格式** | PEM 格式（证书与私钥） |
| **导出文件扩展名** | `.crt`（证书）、`.key`（私钥）、`.csr`（签名请求）、`.crl`（吊销列表） |
| **默认有效期** | 根 CA：10 年 / 颁发用 CA：5 年 / 服务器证书（CSR 签发）：1 年 |

---

## 运行环境与技术栈

- **操作系统**：Windows 10 版本 1809（Build 17763）及以上 / Windows 11（x64）
- **运行时 / SDK**：.NET（`net11.0-windows10.0.22621.0`）、Windows App SDK 2.7.0（WinUI 3）
- **开发语言**：C# / XAML
- **数据存储**：
  - SQLite（`Microsoft.Data.Sqlite`）
  - PostgreSQL（`Npgsql`）

---

## 构建与运行

### 使用 Visual Studio

1. 克隆代码仓库：
   ```bash
   git clone <repository-url>
   cd RoughCA
   ```
2. 使用 Visual Studio 打开 `RoughCA.sln`。
3. 将目标平台选择为 `x64`，然后执行构建并运行。

### 使用 .NET CLI

```powershell
dotnet restore RoughCA.sln
dotnet build RoughCA.sln -c Release -p:Platform=x64
```

---

## 快速开始

### 1. 配置数据存储（以 SQLite 为例）

1. 启动应用，从左侧导航栏进入 **设置** 页面。
2. 点击右上角的锁定按钮解除锁定，进入编辑模式。
3. 在 **连接设置** 标签页中选择 `SQLite`，输入配置文件名与数据库名称。
   - 若 SQLite 文件路径留空，应用将默认自动创建并使用 `%APPDATA%\Arteria\RoughCA\{DatabaseName}.db`。
4. 点击 **保存并重新连接**。

### 2. 配置组织信息与 CA 初始化

1. 切换至设置页面的 **CA 信息** 标签页。
2. 填写根 CA 名称（`trustName`）、颁发用 CA 名称（`issueName`）以及组织信息（组织名 `O`、部门名 `OU`、省/州 `ST`、城市 `L`、国家代码 `C`、发布服务器名称）。
3. 信息配置完成后，应用会自动生成尚未创建的根 CA 与颁发用 CA 证书，随后即可开始签发各类证书。

### PostgreSQL 连接参数说明

若选择 PostgreSQL 作为后端数据库，需配置以下参数以建立双向 TLS（mTLS）安全连接：

| 参数 | 说明 |
| :--- | :--- |
| **服务器主机名（HostName）** | PostgreSQL 服务器的主机名（FQDN） |
| **数据库名（DatabaseName）** | 目标数据库名称 |
| **模式名 / 用户名（SchemaName）** | 用于连接数据库的用户名 |
| **客户端私钥（ClientSecretKey）** | mTLS 连接所使用的客户端私钥文件 |
| **客户端证书（ClientCertificate）** | mTLS 连接所使用的客户端证书文件 |
| **根证书（RootCertificate）** | 用于验证 PostgreSQL 服务器证书的受信任根证书文件 |

> 数据库表结构参考脚本位于 [`Scripts/autoca.sql`](Scripts/autoca.sql)。

---

## 项目结构

```text
RoughCA/
├── RoughCA.sln                    # Visual Studio 解决方案
├── Scripts/
│   └── autoca.sql                 # 数据库表结构定义脚本
└── wrk/RoughCA/
    ├── RoughCA.csproj             # WinUI 3 项目配置
    ├── App.xaml(.cs)              # 应用入口
    ├── MainWindow.xaml(.cs)       # 主窗口、导航栏与配置文件切换
    ├── Strings/                   # 多语言资源 (ja-JP / en-US)
    ├── Assets/                    # 应用图标与视觉资源
    └── src/
        ├── CA/                    # CA 核心逻辑 (Authority, Certificate, SignRequest 等)
        ├── Common/                # 配置文件管理、辅助工具与异常定义
        ├── DB/                    # 数据访问层 (SQLiteContext, SQLContext, VSQLContext)
        └── Page/                  # 功能页面 (签发历史、创建、CSR、签名、设置、版本)
```

---

## 许可证

本项目基于 [MIT License](LICENSE) 开源发布。  
Copyright (c) 2024 ArteriaStudio
