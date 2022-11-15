\USE [PDOmahaSpectrum]
GO
/****** Object:  StoredProcedure [dbo].[uspUIImtCallHomeAsJSON]    Script Date: 7/28/2021 8:20:58 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


-- =============================================
-- Author:		Kushal Singh
-- Create date: 7/11/2021
-- Description:	 
--		Grab data for IMT call home console application 

--  SPEC-848

--  EXEC [dbo].[uspUIImtCallHomeAsJSON] 
-- =============================================

ALTER   PROCEDURE [dbo].[uspUIImtCallHomeAsJSON] 
@AsOf date = null
	

AS
BEGIN
	SET NOCOUNT ON;
	IF @AsOf IS NULL
	SET @AsOf = GETDATE()

DECLARE @IMTInfo as TABLE
	(
	company varchar(250) NOT NULL DEFAULT(0000),
	policy_count int NOT NULL DEFAULT(0),
	policy_count_date date NULL DEFAULT(NULL),
	annual_premium decimal(18,2) NOT NULL DEFAULT(0),
	payment_processor varChar(250) NOT NULL DEFAULT('NONE'),
	version varchar(250) NOT NULL DEFAULT(''),
	eft bit NOT NULL DEFAULT(0),
	agency_download varchar(250) NOT NULL DEFAULT(0),
	document_management varchar(250) NOT NULL DEFAULT('docFinity'),
	agent_rater varchar(250) NOT NULL DEFAULT(0),
	lender_notification varChar(250) NOT NULL DEFAULT('NONE')
	)

-- package companies/ organizations name
DECLARE @PackageCompanies as TABLE
	(
	 package_company VarChar(150) NOT NULL DEFAULT('')
	)

DECLARE @PolicyInfo as TABLE 
	(
	agreement_number VarChar(100) NOT NULL DEFAULT(''),
	policy_number VarChar(100) NOT NULL DEFAULT(''), 
	agreement_end_date date NULL DEFAULT(NULL), 
	PRIMARY KEY (agreement_number, policy_number)
	)

INSERT INTO @IMTInfo
 (	company )
SELECT ISNULL(MC.SettingValue,'0000')
FROM DBO.MasterConfig AS MC WITH (NOLOCK)
WHERE MC.SettingName = 'imtCustomerNumber'

UPDATE @IMTInfo
SET version = V.VersionNumber
FROM DBO.Version AS V WITH (NOLOCK)

UPDATE @IMTInfo
SET agency_download = mc.SettingValue	-- Agency download
FROM dbo.MasterConfig AS mc WITH (NOLOCK)
WHERE mc.SettingName ='agencyDownload'

UPDATE @IMTInfo
SET document_management = mc.SettingValue	-- document management
FROM dbo.MasterConfig AS mc WITH (NOLOCK)
WHERE mc.SettingName ='imageTreeControl'

UPDATE @IMTInfo
SET eft =  Selected		--  EFT
FROM DBO.ConfigurationBillingMethodOfPaymentFilter AS F WITH (NOLOCK)
INNER JOIN Enumerations.DBO.ENUMMethodOfPayment AS ENUM WITH (NOLOCK)
	ON F.MethodOfPaymentID = ENUM.ID 
WHERE ENUM.Value = 'EFT' 


INSERT INTO @PackageCompanies
(	package_company	)
SELECT ISNULL(O.OrganizationName, '')
FROM DBO.CoinsurerAccount AS CO WITH (NOLOCK)
INNER JOIN DBO.PartyRole AS Carrier_PR WITH (NOLOCK)
	on Carrier_PR.RoleID = co.Carrier_RoleID
INNER JOIN DBO.Role AS R WITH (NOLOCK)
	ON R.RoleID = CO.Coinsurer_RoleID
INNER JOIN DBO.PartyRole AS PR WITH (NOLOCK)
	ON co.Coinsurer_RoleID = PR.RoleID
INNER JOIN DBO.PartyName AS PN WITH (NOLOCK)
	ON PN.Party_SubjectMatterID = PR.SubjectMatterID
LEFT OUTER JOIN DBO.OrganizationName AS O WITH (NOLOCK)
	ON O.PartyNameID = PN.PartyNameID
WHERE CO.Carrier_RoleID IS NOT NULL


INSERT INTO @PolicyInfo
(	  agreement_number	)
select distinct AgreementNumberText
from (
SELECT DISTINCT a.agreementnumbertext
,RANK() OVER 
    (PARTITION BY PolicyNumberText ORDER BY a.agreementid DESC) AS Ranking
FROM DBO.Agreement AS A WITH (NOLOCK)
INNER JOIN DBO.Contract AS K WITH (NOLOCK)
	ON A.AgreementNumberText = K.AgreementNumberText
WHERE A.AgreementEndDate >= @AsOf
AND (A.AgreementStatus IN (1, 3, 4, 5, 6) OR (A.AgreementStatus = 0 AND K.ContractStatusCode = 13))
AND K.ContractStatusCode <> 7
AND A.AgreementCommitDate IS NOT NULL
AND ISNULL(A.AGREEMENTDELETEDATE, dateadd(day, 1,@AsOf)) > @AsOf
) as tmp
where Ranking = 1

DELETE FROM @PolicyInfo
WHERE agreement_number IS NULL


UPDATE @IMTInfo
SET policy_count = -- total policy count
(SELECT COUNT(P.agreement_number)
FROM @PolicyInfo AS P)

UPDATE @IMTInfo		-- policy count date
set policy_count_date = @AsOf

UPDATE @IMTInfo
SET annual_premium =			--Annual Premium
(SELECT SUM(CP.RatedPremium)
FROM @PolicyInfo AS P
INNER JOIN DBO.ContractCoveragePremiums AS CP WITH (NOLOCK)
	ON P.agreement_number = CP.ContractAgreementNumberText
INNER JOIN DBO.Agreement AS A WITH (NOLOCK)
	ON A.AgreementNumberText = P.agreement_number
INNER JOIN DBO.[Contract] AS K WITH (NOLOCK)
	ON K.AgreementNumberText = P.agreement_number
)

UPDATE @IMTInfo
SET lender_notification = 'InsVista'		-- lender notification
FROM DBO.InVistaAccountInfo AS IA WITH (NOLOCK)
WHERE (IA.NaicNumber IS NOT NULL AND IA.NaicNumber <> '') OR
	  (IA.[FileName] IS NOT NULL AND IA.[FileName] <> '') OR
	  (IA.FEIN IS NOT NULL AND IA.FEIN <> '')

SELECT
	IMT.company,
	IMT.policy_count,
	IMT.policy_count_date,
	IMT.version,
	IMT.eft ,
	IMT.annual_premium ,
	IMT.payment_processor,
	IMT.agency_download ,
	IMT.document_management,
	IMT.agent_rater,
	IMT.lender_notification ,
	(SELECT PC.package_company AS name
	FROM @PackageCompanies AS PC
	FOR JSON PATH) as package_company
FROM @IMTInfo AS IMT, @PackageCompanies PC
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER

END

