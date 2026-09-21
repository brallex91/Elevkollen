// Thin IndexedDB wrapper. All student data stays in the browser and never leaves the device.
const DB_NAME = 'elevkollen';
const DB_VERSION = 2;

// The database was named 'shoolplanner' before the rename. The old one is not left behind
// cluttering the browser profile, but its contents are lost -- back up first.
const LEGACY_DB_NAME = 'shoolplanner';

let dbPromise = null;

function open() {
    dbPromise ??= new Promise((resolve, reject) => {
        indexedDB.deleteDatabase(LEGACY_DB_NAME);

        const request = indexedDB.open(DB_NAME, DB_VERSION);

        request.onupgradeneeded = () => {
            const db = request.result;

            if (!db.objectStoreNames.contains('students')) {
                const students = db.createObjectStore('students', { keyPath: 'id', autoIncrement: true });
                students.createIndex('className', 'className');
            }

            if (!db.objectStoreNames.contains('assessments')) {
                const assessments = db.createObjectStore('assessments', { keyPath: 'id', autoIncrement: true });
                assessments.createIndex('studentId', 'studentId');
            }

            // v2: key/value for app data that is not personal data (last export, syllabus cache).
            if (!db.objectStoreNames.contains('meta')) {
                db.createObjectStore('meta', { keyPath: 'key' });
            }
        };

        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });

    return dbPromise;
}

async function tx(stores, mode, work) {
    const db = await open();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(stores, mode);
        const result = work(...stores.map(s => transaction.objectStore(s)));
        transaction.oncomplete = () => resolve(result.value);
        transaction.onerror = () => reject(transaction.error);
        transaction.onabort = () => reject(transaction.error);
    });
}

function request(req, map = x => x) {
    const box = { value: undefined };
    req.onsuccess = () => box.value = map(req.result);
    return box;
}

// Without an id, IndexedDB assigns the key itself via autoIncrement.
function strip(entity) {
    if (entity.id === null || entity.id === undefined || entity.id === 0) {
        const { id, ...rest } = entity;
        return rest;
    }
    return entity;
}

export function getStudents() {
    return tx(['students'], 'readonly', store => request(store.getAll()));
}

export function getStudent(id) {
    return tx(['students'], 'readonly', store => request(store.get(id)));
}

export function putStudent(student) {
    return tx(['students'], 'readwrite', store => request(store.put(strip(student))));
}

export function deleteStudent(id) {
    return tx(['students', 'assessments'], 'readwrite', (students, assessments) => {
        students.delete(id);
        // Cascade delete: a student's assessments must never be orphaned.
        const cursor = assessments.index('studentId').openCursor(IDBKeyRange.only(id));
        cursor.onsuccess = () => {
            const c = cursor.result;
            if (c) {
                c.delete();
                c.continue();
            }
        };
        return { value: true };
    });
}

export function getAssessments(studentId) {
    return tx(['assessments'], 'readonly', store =>
        request(store.index('studentId').getAll(IDBKeyRange.only(studentId))));
}

export function getAllAssessments() {
    return tx(['assessments'], 'readonly', store => request(store.getAll()));
}

export function putAssessment(assessment) {
    return tx(['assessments'], 'readwrite', store => request(store.put(strip(assessment))));
}

export function deleteAssessment(id) {
    return tx(['assessments'], 'readwrite', store => request(store.delete(id), () => true));
}

/// Several assessments in one transaction: the whole class is saved, or none of it.
export function putAssessments(assessments) {
    return tx(['assessments'], 'readwrite', store => {
        for (const a of assessments) {
            store.put(strip(a));
        }
        return { value: assessments.length };
    });
}

/// Assessments belonging to one and the same occasion: subject + work area + date.
export function findAssessments(subjectCode, workArea, date) {
    const area = workArea || null;
    return tx(['assessments'], 'readonly', store =>
        request(store.getAll(), all => all.filter(a =>
            a.subjectCode === subjectCode
            && (a.workArea || null) === area
            && a.date === date)));
}

export function deleteAssessments(ids) {
    return tx(['assessments'], 'readwrite', store => {
        for (const id of ids) {
            store.delete(id);
        }
        return { value: ids.length };
    });
}

export function getMeta(key) {
    return tx(['meta'], 'readonly', store => request(store.get(key), r => r?.value ?? null));
}

export function setMeta(key, value) {
    return tx(['meta'], 'readwrite', store => request(store.put({ key, value }), () => true));
}

// New backups carry the current name. Backups taken before the rename must keep working
// on import -- they are the only way back for data that lived in the old database.
const BACKUP_FORMAT = 'elevkollen-backup';
const BACKUP_FORMATS_ACCEPTED = [BACKUP_FORMAT, 'shoolplanner-backup'];

export async function exportAll() {
    return JSON.stringify({
        format: BACKUP_FORMAT,
        version: 1,
        exportedAt: new Date().toISOString(),
        students: await getStudents(),
        assessments: await getAllAssessments(),
    });
}

/// Replaces all data in a single transaction: either the whole import succeeds or none of it.
export async function importAll(json) {
    const data = JSON.parse(json);

    if (!BACKUP_FORMATS_ACCEPTED.includes(data?.format) || !Array.isArray(data.students)) {
        throw new Error('CONTENT');
    }

    await tx(['students', 'assessments'], 'readwrite', (students, assessments) => {
        students.clear();
        assessments.clear();
        for (const s of data.students) {
            students.put(s);
        }
        for (const a of data.assessments ?? []) {
            assessments.put(a);
        }
        return { value: true };
    });

    return { students: data.students.length, assessments: (data.assessments ?? []).length };
}

/// Counts rows without reading them — MainLayout asks on every sign-in.
export function counts() {
    return tx(['students', 'assessments'], 'readonly', (students, assessments) => {
        const s = request(students.count());
        const a = request(assessments.count());
        return { get value() { return { students: s.value, assessments: a.value }; } };
    });
}
