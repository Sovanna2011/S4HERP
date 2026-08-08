sap.ui.define([
    "sap/ui/core/Messaging",
    "sap/ui/core/message/Message",
    "sap/ui/core/message/MessageType"
], (Messaging, Message, MessageType) => {
    "use strict";

    /**
     * Single point of contact with the S4HERP API.
     *
     * Every failure arrives as RFC 7807, so field-level violations are turned
     * into MessageManager entries bound to the field that caused them — which is
     * what lets the UI put the message beside the input rather than in a generic
     * dialog.
     */
    return {
        async request(path, options) {
            const settings = options || {};
            const context = settings.context || {};

            // The payment file is XML, and everything else is JSON. Asking for
            // JSON on the file would be a lie in the Accept header and a parse
            // error in the response.
            const headers = {
                "Accept": settings.raw ? "*/*" : "application/json",
                "X-S4HERP-User": context.userName || ""
            };
            if (settings.body) {
                headers["Content-Type"] = "application/json";
            }
            if (settings.idempotencyKey) {
                headers["Idempotency-Key"] = settings.idempotencyKey;
            }

            const response = await fetch(path, {
                method: settings.method || "GET",
                headers,
                body: settings.body ? JSON.stringify(settings.body) : undefined
            });

            const text = await response.text();

            // A failure is RFC 7807 JSON whatever the request asked for, so the
            // error path parses even when the success path would not.
            if (response.ok) {
                return settings.raw ? text : (text ? JSON.parse(text) : null);
            }

            let problem = null;
            try {
                problem = text ? JSON.parse(text) : null;
            } catch {
                problem = { detail: text || null };
            }

            throw this._toError(response.status, problem);
        },

        _toError(status, problem) {
            const error = new Error((problem && problem.detail) || "The request failed.");
            error.status = status;
            error.errorCode = problem && problem.errorCode;
            error.correlationId = problem && problem.correlationId;
            error.violations = [];

            if (problem && problem.errors) {
                Object.keys(problem.errors).forEach((field) => {
                    problem.errors[field].forEach((v) => {
                        error.violations.push({
                            field,
                            code: v.code,
                            message: v.message
                        });
                    });
                });
            }
            return error;
        },

        /** Publishes an API error into the message model behind the MessagePopover. */
        report(error) {
            Messaging.removeAllMessages();

            if (error.violations && error.violations.length) {
                error.violations.forEach((v) => {
                    Messaging.addMessages(new Message({
                        message: v.message,
                        type: MessageType.Error,
                        additionalText: v.field,
                        code: v.code,
                        processor: undefined
                    }));
                });
                return;
            }

            Messaging.addMessages(new Message({
                message: error.message,
                type: MessageType.Error,
                additionalText: error.errorCode,
                description: error.correlationId
                    ? "Correlation id: " + error.correlationId
                    : undefined
            }));
        }
    };
});
