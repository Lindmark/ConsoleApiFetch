IF DB_ID('db1') IS NULL
BEGIN
	CREATE DATABASE db1;
END

ELSE
BEGIN
	print('db1 already created')
END

IF DB_ID('db2') IS NULL
BEGIN
	CREATE DATABASE db2;
END

ELSE
BEGIN
	print('db2 already created')
END

IF DB_ID('db1') IS NOT NULL
BEGIN
	USE [db1];
END

IF SCHEMA_ID('sch1') IS NULL
BEGIN
	EXEC('CREATE SCHEMA [sch1] AUTHORIZATION [dbo]')
END

ELSE
BEGIN
	print('sch1 already created')
END

IF OBJECT_ID('db1.sch1.table1', 'U') IS NULL
BEGIN
	CREATE TABLE db1.sch1.table1(
	tKey INT,
	tValue1 VARCHAR(50),
	tValue2 INT
	);
END

ELSE
BEGIN
	print('table1 already created')
	print(OBJECT_ID('db1.sch1.table1', 'U'))
END

IF SCHEMA_ID('sch2') IS NULL
BEGIN
	EXEC('CREATE SCHEMA [sch2] AUTHORIZATION [dbo]')
END

ELSE
BEGIN
	print('sch2 already created')
END

IF OBJECT_ID('db1.sch2.table1', 'U') IS NULL
BEGIN
	CREATE TABLE db1.sch2.table1(
	tKey INT,
	tValue1 VARCHAR(50),
	tValue2 INT
	);
END

ELSE
BEGIN
	print('table2 already created')
	print(OBJECT_ID('db1.sch2.table2', 'U'))
END

IF DB_ID('db2') IS NOT NULL
BEGIN
	USE [db2];
END

IF SCHEMA_ID('sch1') IS NULL
BEGIN
	EXEC('CREATE SCHEMA [sch1] AUTHORIZATION [dbo]')
END

IF OBJECT_ID('db2.sch1.table1', 'U') IS NULL
BEGIN
	CREATE TABLE db2.sch1.table1(
	tKey INT,
	tValue1 VARCHAR(50),
	tValue2 INT
	);
END

ELSE
BEGIN
	print('table1 already created')
	print(OBJECT_ID('db2.sch1.table1', 'U'))
END

IF SCHEMA_ID('sch2') IS NULL
BEGIN
	EXEC('CREATE SCHEMA [sch2] AUTHORIZATION [dbo]')
END

IF OBJECT_ID('db2.sch2.table1', 'U') IS NULL
BEGIN
	CREATE TABLE db2.sch2.table1(
	tKey INT,
	tValue1 VARCHAR(50),
	tValue2 INT
	);
END

ELSE
BEGIN
	print('table1 already created')
	print(OBJECT_ID('db2.sch2.table2', 'U'))
END
/*
*/