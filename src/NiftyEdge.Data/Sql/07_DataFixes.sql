-- Removes candles that were stored from Yahoo Finance's trailing live-snapshot entry rather than a real
-- bar. While a session is open, Yahoo appends an extra element stamped with the request time
-- (meta.regularMarketTime), which landed in Candles as an off-grid duplicate of the bar being formed --
-- e.g. 09:45:58 alongside the genuine 09:45:00 -- holding only a few seconds of trade. Those rows became
-- the newest "completed" bar for the dashboard LTP and for strategy evaluation.
--
-- Every timeframe in use (15m, 60m, 240m) opens its bars on a 15-minute wall-clock boundary with zero
-- seconds, for NSE session-aligned bars as well as Binance's UTC-aligned ones, so anything off that grid
-- is a snapshot artefact. The provider no longer produces these; this clears what was already persisted
-- and is a no-op on subsequent runs.
DELETE FROM
    [dbo].[Candles]
WHERE
    DATEPART(SECOND, CandleTime) <> 0
    OR DATEPART(MINUTE, CandleTime) % 15 <> 0;
GO
