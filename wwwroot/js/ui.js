// Ayudas UI de la pantalla de venta.

// Beep breve para feedback audible ante producto no encontrado / sin stock.
// Usa Web Audio API para no depender de un archivo .mp3.
let audioCtx = null;

export function beep(frecuencia = 660, ms = 150) {
    try {
        audioCtx ??= new (window.AudioContext || window.webkitAudioContext)();
        const osc = audioCtx.createOscillator();
        const gain = audioCtx.createGain();
        osc.type = 'sine';
        osc.frequency.value = frecuencia;
        gain.gain.value = 0.15;
        osc.connect(gain).connect(audioCtx.destination);
        osc.start();
        osc.stop(audioCtx.currentTime + ms / 1000);
    } catch { /* silencio si el navegador no permite audio */ }
}

// Enfoca un elemento por id/selector (util para autofocus tras render).
export function enfocar(selector) {
    const el = typeof selector === 'string'
        ? document.querySelector(selector)
        : selector;
    if (el && typeof el.focus === 'function') el.focus();
}
