// Small browser helpers used by the Blazor client through JS interop.
// Everything here is UI plumbing (storage, clipboard, keyboard, sound); no game rules.
window.wordDuel = (() => {
    let keyHandler = null;
    let audio = null;

    const safe = (fn, fallback) => {
        try { return fn(); } catch { return fallback; }
    };

    const store = kind => (kind === "session" ? window.sessionStorage : window.localStorage);

    return {
        removeSplash() {
            document.getElementById("boot-splash")?.remove();
        },

        storageGet(kind, key) {
            return safe(() => store(kind).getItem(key), null);
        },

        storageSet(kind, key, value) {
            safe(() => store(kind).setItem(key, value));
        },

        storageRemove(kind, key) {
            safe(() => store(kind).removeItem(key));
        },

        async copy(text) {
            try {
                await navigator.clipboard.writeText(text);
                return true;
            } catch {
                // Fallback for non-secure contexts (e.g. http:// on a phone over the LAN).
                const area = document.createElement("textarea");
                area.value = text;
                area.style.position = "fixed";
                area.style.opacity = "0";
                document.body.appendChild(area);
                area.select();
                const ok = safe(() => document.execCommand("copy"), false);
                area.remove();
                return ok;
            }
        },

        // Physical keyboard → .NET. Typing inside form fields is left alone.
        attachKeyboard(dotnetRef) {
            this.detachKeyboard();
            keyHandler = event => {
                if (event.ctrlKey || event.metaKey || event.altKey || event.repeat && event.key !== "Backspace") return;
                const tag = (event.target?.tagName || "").toLowerCase();
                if (tag === "input" || tag === "textarea" || event.target?.isContentEditable) return;

                let key = null;
                if (event.key === "Enter") key = "ENTER";
                else if (event.key === "Backspace") key = "BACK";
                else if (/^[a-zA-Z]$/.test(event.key)) key = event.key.toUpperCase();
                if (!key) return;

                if (key === "ENTER" && tag === "button") return; // let a focused button handle Enter
                event.preventDefault();
                dotnetRef.invokeMethodAsync("OnPhysicalKey", key);
            };
            document.addEventListener("keydown", keyHandler);
        },

        detachKeyboard() {
            if (keyHandler) document.removeEventListener("keydown", keyHandler);
            keyHandler = null;
        },

        // Original, generated sound effects (Web Audio oscillators, no audio files).
        play(name) {
            safe(() => {
                audio = audio || new (window.AudioContext || window.webkitAudioContext)();
                const notes = {
                    type: [[660, 0.03]],
                    reveal: [[523, 0.07], [659, 0.07], [784, 0.09]],
                    error: [[196, 0.12], [165, 0.14]],
                    win: [[523, 0.1], [659, 0.1], [784, 0.1], [1047, 0.22]],
                    lose: [[392, 0.14], [330, 0.14], [262, 0.26]],
                    join: [[440, 0.08], [880, 0.12]],
                    tick: [[1200, 0.02]]
                }[name] || [];
                let at = audio.currentTime;
                for (const [freq, length] of notes) {
                    const osc = audio.createOscillator();
                    const gain = audio.createGain();
                    osc.type = name === "error" || name === "lose" ? "triangle" : "sine";
                    osc.frequency.value = freq;
                    gain.gain.setValueAtTime(0.0001, at);
                    gain.gain.exponentialRampToValueAtTime(0.12, at + 0.01);
                    gain.gain.exponentialRampToValueAtTime(0.0001, at + length);
                    osc.connect(gain).connect(audio.destination);
                    osc.start(at);
                    osc.stop(at + length + 0.02);
                    at += length * 0.9;
                }
            });
        },

        vibrate(ms) {
            safe(() => navigator.vibrate && navigator.vibrate(ms));
        }
    };
})();
