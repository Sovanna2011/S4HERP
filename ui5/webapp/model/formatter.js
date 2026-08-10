sap.ui.define(["sap/ui/core/format/NumberFormat"], (NumberFormat) => {
    "use strict";

    const amount = NumberFormat.getFloatInstance({
        minFractionDigits: 2, maxFractionDigits: 2, groupingEnabled: true
    });

    return {
        /** Amounts are stored signed; the ledger view shows magnitude with an indicator. */
        amount(value) {
            if (value === null || value === undefined || value === "") {
                return "";
            }
            return amount.format(Math.abs(Number(value)));
        },

        signedAmount(value) {
            if (value === null || value === undefined || value === "") {
                return "";
            }
            return amount.format(Number(value));
        },

        debitCreditState(indicator) {
            return indicator === "D" ? "Information" : "Success";
        },

        differenceState(value) {
            return Number(value) === 0 ? "Success" : "Error";
        },

        documentStatusState(status) {
            switch (status) {
                case "Posted": return "Success";
                case "Reversed": return "Warning";
                case "Rejected":
                case "Cancelled": return "Error";
                default: return "None";
            }
        },

        workflowStatusState(status) {
            switch (status) {
                case "Approved": return "Success";
                case "Rejected": return "Error";
                case "Withdrawn": return "Warning";
                default: return "Information";
            }
        },

        /**
         * A bank change request's own status, which is not the same vocabulary as
         * a document's: Applied rather than Posted, and Withdrawn is a normal
         * ending rather than a warning about a document that went nowhere.
         */
        bankChangeStatusState(status) {
            switch (status) {
                case "Applied": return "Success";
                case "Rejected": return "Error";
                case "Withdrawn": return "Warning";
                case "Pending": return "Information";
                default: return "None";
            }
        },

        /**
         * A payment run's lifecycle, which is longer than a document's: Proposed
         * is neutral because nothing has happened yet, PendingApproval is a
         * warning because somebody is being waited on, and Executed is the only
         * success — money has actually moved.
         */
        paymentRunStatusState(status) {
            switch (status) {
                case "Executed": return "Success";
                case "Approved": return "Success";
                case "PendingApproval": return "Warning";
                case "Rejected": return "Error";
                case "Deleted": return "None";
                default: return "Information";
            }
        },

        /**
         * The bank's verdict. Rejected is an error rather than a warning because
         * it means the ledger and the bank disagree about whether money moved,
         * and Pending is a warning rather than neutral because an instruction the
         * bank has not decided on is not finished business.
         */
        bankStatusState(status) {
            switch (status) {
                case "Settled": return "Success";
                case "Accepted": return "Success";
                case "Pending": return "Warning";
                case "Rejected": return "Error";
                default: return "None";
            }
        },

        /** A UTC instant as a plain date and time; the list only needs the day. */
        dateTime(value) {
            if (!value) {
                return "";
            }
            return String(value).replace("T", " ").slice(0, 16);
        },

        stepDecisionState(decision) {
            switch (decision) {
                case "Approved": return "Success";
                case "Rejected": return "Error";
                case "Skipped": return "None";
                default: return "Information";
            }
        }
    };
});
