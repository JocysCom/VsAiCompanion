-- Returns: &Param1=Value+1
SELECT dbo.UrlEncodeKeyValue('Param1', 'Value 1')

-- Returns: Fuerza Aerea (Edificio Condor) Heliport
SELECT dbo.ConvertToASCII('Fuerza Aérea (Edificio Cóndor) Heliport')

-- Returns: Fuerza_Aerea_Edificio_Condor_Heliport
SELECT dbo.GetTitleKey('Fuerza Aérea (Edificio Cóndor) Heliport', 0)
