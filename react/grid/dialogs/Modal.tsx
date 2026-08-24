// Minimal modal kit shared by the grid dialogs. Esc closes; Enter accepts
// (when the dialog wires it); clicks on the overlay close.

import React, { useEffect, useRef } from "react";

export interface ModalProps {
  title: string;
  onClose: () => void;
  onAccept?: () => void;
  acceptLabel?: string;
  children: React.ReactNode;
}

export function Modal(props: ModalProps): JSX.Element {
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    // focus the first input so typing starts immediately
    const input = panelRef.current?.querySelector<HTMLInputElement>("input");
    (input ?? panelRef.current)?.focus();
  }, []);

  function onKeyDown(e: React.KeyboardEvent): void {
    if (e.key === "Escape") {
      e.stopPropagation();
      props.onClose();
    } else if (e.key === "Enter" && props.onAccept) {
      e.stopPropagation();
      props.onAccept();
    }
  }

  return (
    <div
      className="sw-modal-overlay"
      onMouseDown={(e) => {
        if (e.target === e.currentTarget) props.onClose();
      }}
    >
      <div
        className="sw-modal"
        ref={panelRef}
        tabIndex={-1}
        onKeyDown={onKeyDown}
        role="dialog"
        aria-label={props.title}
      >
        <div className="sw-modal-title">{props.title}</div>
        <div className="sw-modal-body">{props.children}</div>
        <div className="sw-modal-buttons">
          <button className="sw-modal-button" onClick={props.onClose}>
            Cancel
          </button>
          {props.onAccept && (
            <button className="sw-modal-button sw-primary" onClick={props.onAccept}>
              {props.acceptLabel ?? "OK"}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
