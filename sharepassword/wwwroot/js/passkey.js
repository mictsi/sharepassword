(function () {
    function base64UrlToBuffer(value) {
        const base64 = value.replace(/-/g, "+").replace(/_/g, "/");
        const padded = base64 + "=".repeat((4 - (base64.length % 4)) % 4);
        const binary = window.atob(padded);
        const bytes = new Uint8Array(binary.length);
        for (let index = 0; index < binary.length; index += 1) {
            bytes[index] = binary.charCodeAt(index);
        }
        return bytes.buffer;
    }

    function bufferToBase64Url(buffer) {
        const bytes = new Uint8Array(buffer);
        let binary = "";
        for (let index = 0; index < bytes.length; index += 1) {
            binary += String.fromCharCode(bytes[index]);
        }
        return window.btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
    }

    function hasWebAuthn() {
        return Boolean(window.PublicKeyCredential && navigator.credentials);
    }

    function getAntiForgeryToken() {
        const field = document.querySelector('input[name="__RequestVerificationToken"]');
        return field ? field.value : "";
    }

    async function postJson(url, body) {
        const response = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": getAntiForgeryToken()
            },
            body: body === undefined ? "{}" : JSON.stringify(body)
        });

        if (!response.ok) {
            const text = await response.text();
            throw new Error(text || `Request failed (${response.status}).`);
        }

        return response.json();
    }

    function decodeCreationOptions(options) {
        options.challenge = base64UrlToBuffer(options.challenge);
        options.user.id = base64UrlToBuffer(options.user.id);
        (options.excludeCredentials || []).forEach((credential) => {
            credential.id = base64UrlToBuffer(credential.id);
        });
        return options;
    }

    function decodeRequestOptions(options) {
        options.challenge = base64UrlToBuffer(options.challenge);
        (options.allowCredentials || []).forEach((credential) => {
            credential.id = base64UrlToBuffer(credential.id);
        });
        return options;
    }

    function encodeAttestationResponse(credential) {
        return {
            id: credential.id,
            rawId: bufferToBase64Url(credential.rawId),
            type: credential.type,
            response: {
                attestationObject: bufferToBase64Url(credential.response.attestationObject),
                clientDataJSON: bufferToBase64Url(credential.response.clientDataJSON),
                transports: typeof credential.response.getTransports === "function"
                    ? credential.response.getTransports()
                    : []
            },
            clientExtensionResults: credential.getClientExtensionResults()
        };
    }

    function encodeAssertionResponse(credential) {
        return {
            id: credential.id,
            rawId: bufferToBase64Url(credential.rawId),
            type: credential.type,
            response: {
                authenticatorData: bufferToBase64Url(credential.response.authenticatorData),
                clientDataJSON: bufferToBase64Url(credential.response.clientDataJSON),
                signature: bufferToBase64Url(credential.response.signature),
                userHandle: credential.response.userHandle
                    ? bufferToBase64Url(credential.response.userHandle)
                    : null
            },
            clientExtensionResults: credential.getClientExtensionResults()
        };
    }

    function showError(element, message) {
        if (element) {
            element.textContent = message;
            element.classList.remove("d-none");
        }
    }

    function clearError(element) {
        if (element) {
            element.classList.add("d-none");
        }
    }

    function setupLogin() {
        const container = document.querySelector("[data-passkey-login]");
        if (!container) {
            return;
        }

        const button = document.getElementById("passkeyLoginButton");
        const errorBox = document.getElementById("passkeyLoginError");

        async function signIn() {
            clearError(errorBox);

            if (!hasWebAuthn()) {
                showError(errorBox, "This browser does not support passkeys.");
                return;
            }

            button.disabled = true;
            try {
                const options = await postJson(container.dataset.passkeyOptionsUrl);
                const credential = await navigator.credentials.get({
                    publicKey: decodeRequestOptions(options)
                });

                const result = await postJson(container.dataset.passkeyVerifyUrl, {
                    response: JSON.stringify(encodeAssertionResponse(credential)),
                    returnUrl: container.dataset.passkeyReturnUrl || null
                });

                if (result.succeeded && result.redirectUrl) {
                    window.location.assign(result.redirectUrl);
                    return;
                }

                showError(errorBox, result.error || "Passkey sign-in failed.");
            } catch (error) {
                if (error && (error.name === "NotAllowedError" || error.name === "AbortError")) {
                    showError(errorBox, "The passkey prompt was cancelled or timed out.");
                } else {
                    showError(errorBox, error && error.message ? error.message : "Passkey sign-in failed.");
                }
            } finally {
                button.disabled = false;
            }
        }

        button?.addEventListener("click", signIn);
    }

    function setupRegistration() {
        const container = document.querySelector("[data-passkey-register]");
        if (!container) {
            return;
        }

        const button = document.getElementById("passkeyRegisterButton");
        const nameInput = document.getElementById("passkeyDisplayName");
        const errorBox = document.getElementById("passkeyRegisterError");

        async function register() {
            clearError(errorBox);

            if (!hasWebAuthn()) {
                showError(errorBox, "This browser does not support passkeys.");
                return;
            }

            button.disabled = true;
            try {
                const options = await postJson(container.dataset.passkeyOptionsUrl);
                const credential = await navigator.credentials.create({
                    publicKey: decodeCreationOptions(options)
                });

                const result = await postJson(container.dataset.passkeyRegisterUrl, {
                    response: JSON.stringify(encodeAttestationResponse(credential)),
                    displayName: nameInput ? nameInput.value : "",
                    returnUrl: container.dataset.passkeyReturnUrl || null
                });

                if (result.succeeded && result.redirectUrl) {
                    window.location.assign(result.redirectUrl);
                    return;
                }

                if (result.succeeded) {
                    window.location.reload();
                    return;
                }

                showError(errorBox, result.error || "Passkey registration failed.");
            } catch (error) {
                if (error && error.name === "InvalidStateError") {
                    showError(errorBox, "This authenticator is already registered for the account.");
                } else if (error && (error.name === "NotAllowedError" || error.name === "AbortError")) {
                    showError(errorBox, "The passkey prompt was cancelled or timed out.");
                } else {
                    showError(errorBox, error && error.message ? error.message : "Passkey registration failed.");
                }
            } finally {
                button.disabled = false;
            }
        }

        button?.addEventListener("click", register);
    }

    setupLogin();
    setupRegistration();
})();
