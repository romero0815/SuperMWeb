// Modulo ES para captura del lector de codigo de barras.
// Escucha keydown a nivel documento, acumula caracteres imprimibles hasta
// detectar el sufijo (default "Enter") y emite la cadena completa via la
// referencia .NET.
//
// Un lector HID de teclado emite todos los caracteres del codigo muy rapido
// seguidos de un sufijo programable. Aqui NO se usa timing como mecanismo
// principal (segun regla del prompt), pero se mantiene un timeout de 500ms
// para descartar buffers viejos que quedaron pegados si el foco cambio en
// medio del escaneo.

let handler = null;

export function iniciar(dotNetRef, sufijo, prefijo) {
    if (handler) detener(); // idempotente: si ya hay handler, reiniciar
    sufijo = sufijo || 'Enter';
    prefijo = prefijo || '';

    let buffer = '';
    let ultima = Date.now();
    let prefijoOk = prefijo.length === 0;

    const TIMEOUT_MS = 500;

    handler = (ev) => {
        // Reset del buffer si paso mucho tiempo desde la ultima tecla.
        if (Date.now() - ultima > TIMEOUT_MS) {
            buffer = '';
            prefijoOk = prefijo.length === 0;
        }
        ultima = Date.now();

        // Detectar sufijo -> emitir
        if (ev.key === sufijo) {
            if (buffer.length > 0 && prefijoOk) {
                ev.preventDefault();
                const codigo = buffer;
                buffer = '';
                prefijoOk = prefijo.length === 0;
                dotNetRef.invokeMethodAsync('OnEscaneoJs', codigo);
            }
            return;
        }

        // Ignorar teclas de control (Tab, Shift, Alt, etc.) que no son caracteres.
        if (ev.key.length !== 1) return;

        // Prefijo: si viene configurado, chequear que el buffer arranque con el.
        if (!prefijoOk) {
            buffer += ev.key;
            if (buffer.startsWith(prefijo)) {
                buffer = buffer.substring(prefijo.length);
                prefijoOk = true;
            } else if (!prefijo.startsWith(buffer)) {
                // El buffer diverge del prefijo esperado: descartar.
                buffer = '';
            }
            return;
        }

        buffer += ev.key;
    };

    document.addEventListener('keydown', handler, true); // capture=true
}

export function detener() {
    if (handler) {
        document.removeEventListener('keydown', handler, true);
        handler = null;
    }
}
