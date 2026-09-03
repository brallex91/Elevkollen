// Kryptering av säkerhetskopior. Allt sker lokalt i webbläsaren via WebCrypto.
// Nyckeln härleds från användarens lösenord — utan lösenordet är filen inte läsbar.

const MAGIC = [0x45, 0x44, 0x4f, 0x4b]; // "EDOK"
const VERSION = 1;
const SALT_BYTES = 16;
const IV_BYTES = 12;
const ITERATIONS = 600000;

async function deriveKey(password, salt) {
    const material = await crypto.subtle.importKey(
        'raw', new TextEncoder().encode(password), 'PBKDF2', false, ['deriveKey']);

    return crypto.subtle.deriveKey(
        { name: 'PBKDF2', salt, iterations: ITERATIONS, hash: 'SHA-256' },
        material,
        { name: 'AES-GCM', length: 256 },
        false,
        ['encrypt', 'decrypt']);
}

/// Filformat: MAGIC(4) | VERSION(1) | SALT(16) | IV(12) | AES-256-GCM-ciphertext
export async function exportEncrypted(json, password, fileName) {
    const salt = crypto.getRandomValues(new Uint8Array(SALT_BYTES));
    const iv = crypto.getRandomValues(new Uint8Array(IV_BYTES));
    const key = await deriveKey(password, salt);

    const cipher = new Uint8Array(await crypto.subtle.encrypt(
        { name: 'AES-GCM', iv }, key, new TextEncoder().encode(json)));

    const file = new Uint8Array(MAGIC.length + 1 + SALT_BYTES + IV_BYTES + cipher.length);
    let offset = 0;
    file.set(MAGIC, offset); offset += MAGIC.length;
    file[offset] = VERSION; offset += 1;
    file.set(salt, offset); offset += SALT_BYTES;
    file.set(iv, offset); offset += IV_BYTES;
    file.set(cipher, offset);

    const url = URL.createObjectURL(new Blob([file], { type: 'application/octet-stream' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
}

/// Läser vald fil och returnerar dekrypterad JSON.
/// Kastar 'FORMAT', 'VERSION' eller 'PASSWORD' så att UI:t kan ge ett begripligt besked.
export async function importEncrypted(inputElement, password) {
    const file = inputElement?.files?.[0];
    if (!file) {
        throw new Error('FORMAT');
    }

    const bytes = new Uint8Array(await file.arrayBuffer());
    const headerLength = MAGIC.length + 1 + SALT_BYTES + IV_BYTES;

    if (bytes.length <= headerLength || !MAGIC.every((b, i) => bytes[i] === b)) {
        throw new Error('FORMAT');
    }

    if (bytes[MAGIC.length] !== VERSION) {
        throw new Error('VERSION');
    }

    let offset = MAGIC.length + 1;
    const salt = bytes.slice(offset, offset += SALT_BYTES);
    const iv = bytes.slice(offset, offset += IV_BYTES);
    const cipher = bytes.slice(offset);

    try {
        const key = await deriveKey(password, salt);
        const plain = await crypto.subtle.decrypt({ name: 'AES-GCM', iv }, key, cipher);
        return new TextDecoder().decode(plain);
    } catch {
        // AES-GCM verifierar integriteten, så fel lösenord och manipulerad fil ser likadana ut.
        throw new Error('PASSWORD');
    }
}

/// Starkt slumpat lösenord som läraren kan spara i sin lösenordshanterare.
export function suggestPassword() {
    const alphabet = 'abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789';
    return [...crypto.getRandomValues(new Uint8Array(24))]
        .map(b => alphabet[b % alphabet.length])
        .join('');
}

export function clearFileInput(inputElement) {
    if (inputElement) {
        inputElement.value = '';
    }
}

export function hasFile(inputElement) {
    return !!inputElement?.files?.length;
}
