// Port of the InputFieldForm usage for regex show/hide and Highlight:
// a text prompt with a live preview callback and a localStorage MRU
// (replacing the WinForms registry MRU).

import React, { useEffect, useMemo, useState } from "react";
import { Modal } from "./Modal";

const MRU_MAX = 10;

function loadMru(key: string): string[] {
  try {
    const raw = window.localStorage.getItem(key);
    const parsed = raw ? JSON.parse(raw) : [];
    return Array.isArray(parsed) ? parsed.filter((x) => typeof x === "string") : [];
  } catch {
    return [];
  }
}

function saveMru(key: string, value: string): void {
  try {
    const mru = loadMru(key).filter((x) => x !== value);
    mru.unshift(value);
    window.localStorage.setItem(key, JSON.stringify(mru.slice(0, MRU_MAX)));
  } catch {
    // localStorage unavailable - MRU is a convenience only
  }
}

export interface RegexPromptDialogProps {
  title: string;
  hint?: string;
  mruKey: string;
  isRegex: boolean; // validate as regex and show errors when true
  onPreview?: (value: string) => void; // fired per keystroke
  onAccept: (value: string) => void;
  onClose: () => void;
}

export function RegexPromptDialog(props: RegexPromptDialogProps): JSX.Element {
  const [value, setValue] = useState("");
  const mru = useMemo(() => loadMru(props.mruKey), [props.mruKey]);

  const regexError = useMemo(() => {
    if (!props.isRegex || value === "") return null;
    try {
      // eslint-disable-next-line no-new
      new RegExp(value, "i");
      return null;
    } catch (e) {
      return (e as Error).message;
    }
  }, [props.isRegex, value]);

  const { onPreview } = props;
  useEffect(() => {
    if (onPreview && regexError === null) {
      onPreview(value);
    }
  }, [value, regexError, onPreview]);

  // clear any live preview when the dialog goes away
  useEffect(() => {
    return () => {
      onPreview?.("");
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function accept(): void {
    if (value === "" || regexError !== null) return;
    saveMru(props.mruKey, value);
    props.onAccept(value);
  }

  return (
    <Modal title={props.title} onClose={props.onClose} onAccept={accept}>
      <input
        type="text"
        value={value}
        onChange={(e) => setValue(e.target.value)}
        spellCheck={false}
      />
      {regexError !== null && <div className="sw-modal-hint">{regexError}</div>}
      {props.hint && <div className="sw-modal-hint">{props.hint}</div>}
      {mru.length > 0 && (
        <div>
          <div className="sw-modal-hint">Recent:</div>
          {mru.map((item) => (
            <div
              key={item}
              className="sw-mru-item"
              onClick={() => setValue(item)}
              title="Click to use"
            >
              {item}
            </div>
          ))}
        </div>
      )}
    </Modal>
  );
}
