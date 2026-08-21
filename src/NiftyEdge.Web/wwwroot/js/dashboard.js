// Dashboard page only: renders the per-instrument candlestick charts and wires the shared
// SignalR connection (created in site.js) to live price ticks and signal events.
(function () {
    "use strict";

    const SIGNAL_STATUS_LABELS = {
        1: "Open",
        2: "Target1Hit",
        3: "Target2Hit",
        4: "StopHit",
        5: "Expired"
    };

    const dataScript = document.getElementById("candle-data");
    const candlesByInstrument = dataScript ? JSON.parse(dataScript.textContent || "{}") : {};
    const charts = {};

    // Lightweight Charts has no time-zone support: it always renders a UTC timestamp as UTC wall-clock,
    // which labelled the 09:15-15:30 IST session as 03:45-10:00. Shifting the timestamps into IST before
    // handing them over is the library's prescribed workaround. IST has no DST, so the offset is constant.
    const IST_OFFSET_SECONDS = 5.5 * 60 * 60;
    const BAR_SECONDS = 15 * 60;

    function toChartTime(utcSeconds) {
        return utcSeconds + IST_OFFSET_SECONDS;
    }

    // NSE 15-minute bars open at 09:15 IST, which is itself on a 15-minute boundary, so flooring in
    // IST-shifted space lands on real bar-open times.
    function floorToBar(chartTime) {
        return Math.floor(chartTime / BAR_SECONDS) * BAR_SECONDS;
    }

    function initCharts() {
        document.querySelectorAll(".chart-container").forEach((container) => {
            const instrumentId = container.id.replace("chart-", "");
            const candles = candlesByInstrument[instrumentId] || [];
            if (!candles.length || typeof LightweightCharts === "undefined") {
                return;
            }

            const chart = LightweightCharts.createChart(container, {
                width: container.clientWidth,
                height: container.clientHeight,
                layout: { background: { color: "transparent" }, textColor: "#4b5262" },
                grid: {
                    vertLines: { color: "#e9ecf5" },
                    horzLines: { color: "#e9ecf5" }
                },
                timeScale: { timeVisible: true, secondsVisible: false },
                rightPriceScale: { borderColor: "#e0e4f0" }
            });

            const series = chart.addCandlestickSeries({
                upColor: "#16a34a",
                downColor: "#e11d48",
                borderVisible: false,
                wickUpColor: "#16a34a",
                wickDownColor: "#e11d48"
            });

            const bars = candles.map((c) => ({
                time: toChartTime(c.Time),
                open: c.Open,
                high: c.High,
                low: c.Low,
                close: c.Close
            }));

            series.setData(bars);
            chart.timeScale().fitContent();

            charts[instrumentId] = { chart, series, lastBar: bars[bars.length - 1] };

            new ResizeObserver(() => chart.applyOptions({ width: container.clientWidth })).observe(container);
        });

        // The newest stored bar is only as recent as the last 15-minute boundary; seed the forming bar from
        // the price the server rendered so the chart starts at the market instead of up to a bar behind it.
        document.querySelectorAll(".instrument-card").forEach((card) => {
            const price = Number(card.dataset.ltp);
            const asOf = Number(card.dataset.ltpTime);
            if (Number.isFinite(price) && price > 0 && Number.isFinite(asOf) && asOf > 0) {
                applyPriceToChart(card.dataset.instrumentId, price, asOf);
            }
        });
    }

    // Folds a live price into the currently forming candle so the chart advances between boundaries
    // instead of staying frozen at whatever was in the database when the page was rendered.
    function applyPriceToChart(instrumentId, price, asOfUtcSeconds) {
        const entry = charts[instrumentId];
        if (!entry) {
            return;
        }

        const barTime = floorToBar(toChartTime(asOfUtcSeconds));
        const lastBar = entry.lastBar;

        if (lastBar && barTime < lastBar.time) {
            return;
        }

        if (!lastBar || barTime > lastBar.time) {
            entry.lastBar = { time: barTime, open: price, high: price, low: price, close: price };
        } else {
            lastBar.high = Math.max(lastBar.high, price);
            lastBar.low = Math.min(lastBar.low, price);
            lastBar.close = price;
        }

        entry.series.update(entry.lastBar);
    }

    function updatePrice(instrumentId, quote) {
        const card = document.querySelector(`.instrument-card[data-instrument-id="${instrumentId}"]`);
        if (!card) {
            return;
        }

        const price = Number(quote.lastTradedPrice ?? quote.LastTradedPrice);
        const ltpEl = card.querySelector(".ltp-value");
        const changeEl = card.querySelector(".change-badge");
        if (ltpEl) {
            ltpEl.textContent = price.toFixed(2);
        }
        if (changeEl) {
            const changePercent = Number(quote.changePercent ?? quote.ChangePercent ?? 0);
            changeEl.textContent = `${changePercent >= 0 ? "+" : ""}${changePercent.toFixed(2)}%`;
            changeEl.classList.toggle("text-bg-success", changePercent >= 0);
            changeEl.classList.toggle("text-bg-danger", changePercent < 0);
        }

        if (Number.isFinite(price) && price > 0) {
            applyPriceToChart(String(instrumentId), price, quoteTimeUtcSeconds(quote));
        }
    }

    function quoteTimeUtcSeconds(quote) {
        const parsed = Date.parse(quote.asOf ?? quote.AsOf ?? "");
        return Math.floor((Number.isNaN(parsed) ? Date.now() : parsed) / 1000);
    }

    function renderSignalCard(signal) {
        const isLong = signal.direction === 1 || signal.Direction === 1;
        const directionLabel = isLong ? "LONG" : "SHORT";
        const timeFrameLabel = (signal.timeFrame ?? signal.TimeFrame) === 15 ? "15m" : "1h";
        const confidence = Number(signal.confidenceScore ?? signal.ConfidenceScore ?? 0);
        const confidenceBarClass = confidence >= 70 ? "bg-success" : confidence >= 40 ? "bg-warning" : "bg-danger";

        const wrapper = document.createElement("div");
        wrapper.className = `signal-card ${isLong ? "signal-long" : "signal-short"} p-3 mb-2 flash`;
        wrapper.dataset.signalId = signal.signalId ?? signal.SignalId;
        wrapper.innerHTML = `
            <div class="d-flex justify-content-between align-items-start flex-wrap gap-1">
                <div>
                    <span class="badge ${isLong ? "text-bg-success" : "text-bg-danger"}">${directionLabel}</span>
                    <span class="fw-semibold ms-1">${signal.strategyName ?? signal.StrategyName}</span>
                    <span class="text-muted small ms-1">(${timeFrameLabel})</span>
                </div>
                <span class="badge bg-info-subtle text-info-emphasis border border-info-subtle signal-status">Open</span>
            </div>
            <div class="signal-levels mt-2 small d-flex flex-wrap column-gap-3 row-gap-1">
                <span>Entry: <strong>${fmt(signal.entryPrice ?? signal.EntryPrice)}</strong></span>
                <span>SL: <strong class="text-danger">${fmt(signal.stopLoss ?? signal.StopLoss)}</strong></span>
                <span>T1: <strong class="text-success">${fmt(signal.target1 ?? signal.Target1)}</strong></span>
                <span>T2: <strong class="text-success">${fmt(signal.target2 ?? signal.Target2)}</strong></span>
                <span>R:R 1:${fmt(signal.riskReward ?? signal.RiskReward)}</span>
            </div>
            <div class="d-flex align-items-center gap-2 mt-2">
                <span class="text-muted small text-nowrap">Confidence</span>
                <div class="progress flex-grow-1" role="progressbar" aria-valuenow="${confidence}" aria-valuemin="0" aria-valuemax="100" style="height: 6px;">
                    <div class="progress-bar ${confidenceBarClass}" style="width: ${confidence}%"></div>
                </div>
                <span class="small fw-semibold text-nowrap">${confidence}%</span>
            </div>
            <div class="signal-rationale text-muted small mt-2">${signal.rationale ?? signal.Rationale}</div>
        `;
        return wrapper;
    }

    function fmt(value) {
        return Number(value).toFixed(2);
    }

    function handleNewSignal(signal) {
        const instrumentId = signal.instrumentId ?? signal.InstrumentId;
        const panel = document.getElementById(`signals-${instrumentId}`);
        if (!panel) {
            return;
        }

        const emptyMessage = panel.querySelector(".no-signal-message");
        if (emptyMessage) {
            emptyMessage.remove();
        }

        panel.prepend(renderSignalCard(signal));
    }

    function handleSignalUpdated(signal) {
        const signalId = signal.signalId ?? signal.SignalId;
        const card = document.querySelector(`.signal-card[data-signal-id="${signalId}"]`);
        if (!card) {
            return;
        }

        const statusValue = signal.status ?? signal.Status;
        const statusEl = card.querySelector(".signal-status");
        if (statusEl) {
            statusEl.textContent = SIGNAL_STATUS_LABELS[statusValue] || "Updated";
        }

        card.classList.add("flash");
        setTimeout(() => card.classList.remove("flash"), 1200);
    }

    document.addEventListener("DOMContentLoaded", initCharts);

    if (window.niftyEdgeConnection) {
        window.niftyEdgeConnection.on("priceUpdate", (instrumentId, quote) => updatePrice(instrumentId, quote));
        window.niftyEdgeConnection.on("newSignal", (signal) => handleNewSignal(signal));
        window.niftyEdgeConnection.on("signalUpdated", (signal) => handleSignalUpdated(signal));
    }
})();
