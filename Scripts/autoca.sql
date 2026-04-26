# 発行した証明書の表
CREATE TABLE TIssuedCerts (
	SequenceMumber	INTEGER 	NOT NULL,
	SerialNumber	TEXT		NOT NULL UNIQUE,
	SubjectMame 	TEXT		NOT NULL,
	CommonMame		TEXT		NOT NULL,
	TypeOf			INTEGER 	NOT NULL,
	Revoked 		INTEGER 	NOT NULL,
	LaunchAt		TEXT		NOT NULL,
	ExpireAt		TEXT		NOT NULL,
	RevokeAt		TEXT,
	AuthorityId 	INTEGER 	NOT NULL,
	CONSTRAINT TIssuedCerts_pkey
		PRIMARY KEY (AuthorityId, SequenceNumber)
);

# 
CREATE TABLE TOrgProfile (
	OrgKey			INTEGER 	NOT NULL,
	OrgName 		TEXT		NOT NULL,
	OrgunitName 	TEXT		NOT NULL,
	LocalityName	TEXT		NOT NULL,
	ProvinceName	TEXT		NOT NULL,
	CountryName 	TEXT		NOT NULL,
	ServerName		TEXT		NOT NULL,
	UpdateAt		TEXT		NOT NULL,

	CONSTRAINT TOrgProfile_pkey PRIMARY KEY (OrgKey)
);

CREATE TABLE TOrgProfile (OrgKey INTEGER NOT NULL, OrgName TEXT NOT NULL, OrgunitName TEXT NOT NULL, LocalityName TEXT NOT NULL, ProvinceName TEXT NOT NULL, CountryName TEXT NOT NULL, ServerName TEXT NOT NULL, UpdateAt TEXT NOT NULL, CONSTRAINT TOrgProfile_pkey PRIMARY KEY (OrgKey));
