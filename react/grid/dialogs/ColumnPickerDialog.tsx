// Port of the CheckBoxListForm column picker: filterable checkbox list of all
// columns, ticked = expanded. The filter box live-tints matching column
// headers in the grid (via onFilterChanged) and OK also reports the first
// match so the grid can move the cursor to it.

import React, { useEffect, useState } from "react";
import { Modal } from "./Modal";

export interface ColumnPickerDialogProps {
  columns: { name: string; checked: boolean }[];
  onFilterChanged?: (filter: string) => void;
  onAccept: (checkedNames: string[], focusColumn: string | null) => void;
  onClose: () => void;
}

export function ColumnPickerDialog(props: ColumnPickerDialogProps): JSX.Element {
  const [filter, setFilter] = useState("");
  const [checked, setChecked] = useState<Set<string>>(
    () => new Set(props.columns.filter((c) => c.checked).map((c) => c.name))
  );

  const { onFilterChanged } = props;
  useEffect(() => {
    onFilterChanged?.(filter);
  }, [filter, onFilterChanged]);

  useEffect(() => {
    return () => {
      onFilterChanged?.("");
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const filterLower = filter.toLowerCase();
  const matches = (name: string): boolean =>
    filter !== "" && name.toLowerCase().includes(filterLower);

  function toggle(name: string): void {
    setChecked((prev) => {
      const next = new Set(prev);
      if (next.has(name)) next.delete(name);
      else next.add(name);
      return next;
    });
  }

  function setAll(to: boolean): void {
    setChecked(to ? new Set(props.columns.map((c) => c.name)) : new Set());
  }

  function accept(): void {
    const focusColumn = props.columns.find((c) => matches(c.name))?.name ?? null;
    props.onAccept(Array.from(checked), focusColumn);
  }

  return (
    <Modal title="Columns" onClose={props.onClose} onAccept={accept}>
      <input
        type="text"
        placeholder="Filter columns..."
        value={filter}
        onChange={(e) => setFilter(e.target.value)}
        spellCheck={false}
      />
      <div>
        <button className="sw-modal-button" onClick={() => setAll(true)}>
          All
        </button>{" "}
        <button className="sw-modal-button" onClick={() => setAll(false)}>
          None
        </button>
      </div>
      <div className="sw-column-list">
        {props.columns.map((c) => (
          <label key={c.name} className={matches(c.name) ? "sw-filter-match" : ""}>
            <input
              type="checkbox"
              checked={checked.has(c.name)}
              onChange={() => toggle(c.name)}
            />
            {c.name}
          </label>
        ))}
      </div>
    </Modal>
  );
}
