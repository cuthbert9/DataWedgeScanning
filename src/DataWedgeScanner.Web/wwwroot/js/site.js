// Optional real-time dashboard updates via SignalR. This script is entirely additive: if the
// SignalR client library failed to load (e.g. this machine has no internet access to reach the
// CDN) or the connection can't be established, the dashboard still shows correct data on every
// normal page load -- nothing here is required for the app to function.
// Auto-submits the "Barcode Scan" field so a real scan reaches the server without a manual
// button tap. Handles both a scanner that sends a terminating Enter keystroke after the barcode,
// and one that sends the characters with no terminator at all (falls back to submitting shortly
// after typing/input pauses). Entirely independent of the TCP listener -- this is a second entry
// point into the same BarcodeScanService.ProcessScanAsync, useful when DataWedge is configured
// for Keystroke Output into this page instead of (or in addition to) IP Output.
(function () {
    "use strict";

    var scanForm = document.querySelector(".manual-scan-form");
    var scanInput = scanForm ? scanForm.querySelector('input[name="ScannedBarcode"]') : null;

    if (scanForm && scanInput) {
        var autoSubmitTimer = null;
        var autoSubmitDelayMs = 400;

        scanInput.addEventListener("keydown", function (event) {
            if (event.key === "Enter") {
                event.preventDefault();
                submitIfNotEmpty();
            }
        });

        scanInput.addEventListener("input", function () {
            window.clearTimeout(autoSubmitTimer);
            autoSubmitTimer = window.setTimeout(submitIfNotEmpty, autoSubmitDelayMs);
        });

        function submitIfNotEmpty() {
            window.clearTimeout(autoSubmitTimer);
            if (scanInput.value.trim().length > 0) {
                scanForm.submit();
            }
        }
    }
})();

(function () {
    "use strict";

    var configEl = document.getElementById("live-update-config");
    if (!configEl) {
        return;
    }

    if (typeof signalR === "undefined") {
        console.warn("SignalR client library not available; live dashboard updates are disabled. The page will still show correct data on refresh.");
        return;
    }

    var hubUrl = configEl.getAttribute("data-hub-url");

    var connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect()
        .build();

    connection.on("ScanProcessed", function (payload) {
        updateItemRow(payload);
        prependRecentScanRow(payload);
    });

    connection.start().catch(function (err) {
        console.warn("Could not connect to the live-update hub; falling back to manual refresh.", err);
    });

    function updateItemRow(payload) {
        if (!payload.itemId) {
            return; // Unknown-barcode scans have no matching item row to update.
        }

        var row = document.querySelector('#items-table-body tr[data-barcode="' + cssEscape(payload.barcode) + '"]');
        if (!row) {
            return; // Item not visible under the current status filter.
        }

        // Column order: Name(0), Quantity(1), Status(2), Description(3), Updated At(4), Barcode(5).
        if (payload.quantity !== null && payload.quantity !== undefined) {
            var quantityValue = row.children[1].querySelector(".quantity-value");
            if (quantityValue) {
                quantityValue.textContent = payload.quantity;
            }
        }

        if (payload.newStatus) {
            var statusCell = row.children[2];
            statusCell.innerHTML = '<span class="status-pill status-' + payload.newStatus.toLowerCase() + '">' + payload.newStatus + "</span>";
        }

        var updatedCell = row.children[4];
        updatedCell.textContent = formatTimestamp(payload.scannedAt);

        flash(row);
    }

    function prependRecentScanRow(payload) {
        var tbody = document.getElementById("recent-scans-table-body");
        if (!tbody) {
            return;
        }

        var emptyRow = tbody.querySelector(".empty-row");
        if (emptyRow) {
            emptyRow.closest("tr").remove();
        }

        var tr = document.createElement("tr");
        tr.innerHTML =
            "<td>" + formatTimestamp(payload.scannedAt) + "</td>" +
            "<td>" + escapeHtml(payload.barcode) + "</td>" +
            "<td>" + escapeHtml(payload.itemName || "-") + "</td>" +
            '<td><span class="result-pill result-' + payload.result.toLowerCase() + '">' + escapeHtml(payload.result) + "</span></td>" +
            "<td>" + escapeHtml(payload.previousStatus || "-") + "</td>" +
            "<td>" + escapeHtml(payload.newStatus || "-") + "</td>";

        tbody.insertBefore(tr, tbody.firstChild);
        flash(tr);

        // Keep the visible list capped to the latest 50 rows, matching the server-rendered page.
        while (tbody.children.length > 50) {
            tbody.removeChild(tbody.lastChild);
        }
    }

    function flash(row) {
        row.classList.remove("row-flash");
        // Force reflow so the animation restarts if the same row updates again quickly.
        void row.offsetWidth;
        row.classList.add("row-flash");
    }

    function formatTimestamp(iso) {
        try {
            return new Date(iso).toLocaleString();
        } catch (e) {
            return iso;
        }
    }

    function escapeHtml(value) {
        var div = document.createElement("div");
        div.textContent = value == null ? "" : String(value);
        return div.innerHTML;
    }

    function cssEscape(value) {
        return window.CSS && CSS.escape ? CSS.escape(value) : value.replace(/"/g, '\\"');
    }
})();
