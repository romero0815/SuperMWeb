const MILES = '.';
const DEC = ',';

function formatear(texto, maxDec) {
    const limpio = texto.replace(/[^\d,]/g, '');
    const i = limpio.indexOf(DEC);

    let ent = i >= 0 ? limpio.slice(0, i) : limpio;
    let dec = i >= 0 ? limpio.slice(i + 1).replace(/,/g, '').slice(0, maxDec) : null;

    ent = ent.replace(/^0+(?=\d)/, '');
    if (ent === '' && dec !== null) ent = '0';
    ent = ent.replace(/\B(?=(\d{3})+(?!\d))/g, MILES);

    return dec !== null ? ent + DEC + dec : ent;
}

function aNumero(texto) {
    const n = parseFloat(texto.replace(/\./g, '').replace(',', '.'));
    return isNaN(n) ? 0 : n;
}

// cuenta los caracteres "reales" (dígitos y coma) antes de una posición
const contar = s => (s.match(/[\d,]/g) || []).length;

export function attach(input, dotNetRef, maxDec, valorInicial) {
    const enviar = () => dotNetRef.invokeMethodAsync('OnValor', aNumero(input.value));

    const onInput = () => {
        const sig = contar(input.value.slice(0, input.selectionStart));
        input.value = formatear(input.value, maxDec);

        // recolocar el cursor después del mismo número de caracteres reales
        let n = 0, p = 0;
        while (p < input.value.length && n < sig) {
            if (input.value[p] !== MILES) n++;
            p++;
        }
        input.setSelectionRange(p, p);
        enviar();
    };

    // el punto (teclado numérico) se convierte en coma decimal
    const onKeydown = e => {
        if (e.key === '.' && !e.ctrlKey && !e.metaKey) {
            e.preventDefault();
            input.setRangeText(DEC, input.selectionStart, input.selectionEnd, 'end');
            onInput();
        }
    };

    const onBlur = () => {
        input.value = input.value.replace(/,$/, '');
    };

    input.addEventListener('input', onInput);
    input.addEventListener('keydown', onKeydown);
    input.addEventListener('blur', onBlur);
    input.__precio = { onInput, onKeydown, onBlur };

    setValue(input, valorInicial, maxDec);
}

export function setValue(input, valor, maxDec = 2) {
    input.value = formatear(String(valor).replace('.', DEC), maxDec);
}

export function detach(input) {
    const h = input.__precio;
    if (!h) return;
    input.removeEventListener('input', h.onInput);
    input.removeEventListener('keydown', h.onKeydown);
    input.removeEventListener('blur', h.onBlur);
}