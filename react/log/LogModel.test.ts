// Fresh behavioural tests for the log port (the C# CsvLog/LogControl have no
// test suites; these pin the semantics the survey documented).

import {
  demoteToDebug,
  extendPath,
  LogEntry,
  newEntry,
  timestampPrefix,
  toDisplayedLine,
  toToolTip,
} from "./LogEntry";
import { LogModel } from "./LogModel";
import { CSV_HEADER, entriesToCsv, entryToCsvRow } from "./csvExport";
import { parseCsvRows } from "../core/csv";

describe("LogEntry", () => {
  test("extendPath appends with a leading colon, innermost first", () => {
    const seen: LogEntry[] = [];
    const sink = extendPath(extendPath((e) => seen.push(e), "GridTab"), "BoundData");
    sink(newEntry("hello"));
    expect(seen[0].callPath).toBe(":BoundData:GridTab");
    // the original entry is not mutated (clone-then-modify)
    const original = newEntry("x");
    sink(original);
    expect(original.callPath).toBe("");
  });

  test("demoteToDebug prefixes only when actually demoting", () => {
    const seen: LogEntry[] = [];
    const sink = demoteToDebug((e) => seen.push(e), "warmup: ");
    sink(newEntry("connecting", "info"));
    expect(seen[0].priority).toBe("debug");
    expect(seen[0].text).toBe("warmup: connecting");
    sink(newEntry("already debug", "debug"));
    expect(seen[1].priority).toBe("debug");
    expect(seen[1].text).toBe("already debug"); // no prefix
  });

  test("displayed line composition", () => {
    const entry = newEntry("message", "info");
    expect(toDisplayedLine(entry)).toBe("[Info] message");
    entry.callPath = ":Grid";
    expect(toDisplayedLine(entry)).toBe("[Info] :Grid - message");
    entry.data = "d1";
    entry.fields = "f1";
    expect(toDisplayedLine(entry)).toBe("[Info] :Grid - message d1 f1");
  });

  test("timestamp prefix shape", () => {
    const prefix = timestampPrefix(new Date(2026, 7, 23, 1, 2, 3, 45).getTime());
    expect(prefix).toBe("2026/08/23 01:02:03.045 ");
  });

  test("tooltip joins split rows and shows source and path", () => {
    const entry = newEntry("line1\nline2", "warn", {
      sourcePath: "c:/src/Foo.cs",
      sourceLineNumber: 42,
      memberName: "DoThing",
    });
    entry.callPath = ":Grid";
    const tip = toToolTip(entry, "line1", true);
    expect(tip).toContain("line1");
    expect(tip).toContain("[Warn] :Grid - line1"); // re-joined full entry
    expect(tip).toContain("c:/src/Foo.cs[42] DoThing");
    expect(tip).toContain("Path: :Grid");
  });
});

describe("LogModel", () => {
  test("filters debug by default (the always-filtering C# default)", () => {
    const m = new LogModel();
    expect(m.filtering).toBe(true);
    m.add(newEntry("d", "debug"));
    m.add(newEntry("i", "info"));
    expect(m.rows.length).toBe(2); // both held
    expect(m.displayedRows.length).toBe(1); // only info shown
    expect(m.displayedRows[0].line).toBe("[Info] i");
  });

  test("threshold is a >= rank comparison", () => {
    const m = new LogModel();
    m.filterPriority = "error";
    m.add(newEntry("w", "warn"));
    m.add(newEntry("e", "error"));
    m.add(newEntry("x", "exception"));
    expect(m.displayedRows.map((r) => r.line)).toEqual(["[Error] e", "[Exception] x"]);
  });

  test("debug threshold with no regex means not filtering", () => {
    const m = new LogModel();
    m.filterPriority = "debug";
    expect(m.filtering).toBe(false);
    m.add(newEntry("d", "debug"));
    expect(m.displayedRows.length).toBe(1);
  });

  test("regex filters displayed lines case-insensitively", () => {
    const m = new LogModel();
    m.filterPriority = "debug";
    m.add(newEntry("Alpha thing"));
    m.add(newEntry("beta thing"));
    m.filterString = "ALPHA";
    expect(m.displayedRows.length).toBe(1);
    expect(m.displayedRows[0].line).toContain("Alpha");
  });

  test("invalid regex sets the error flag and drops the filter", () => {
    const m = new LogModel();
    m.filterString = "([unclosed";
    expect(m.isFilterRegexInvalid).toBe(true);
    m.filterString = "ok";
    expect(m.isFilterRegexInvalid).toBe(false);
  });

  test("multi-line entries split into rows sharing one entry", () => {
    const m = new LogModel();
    m.add(newEntry("first\nsecond\r\nthird"));
    expect(m.rows.length).toBe(3);
    expect(m.rows[0].entry).toBe(m.rows[2].entry);
    expect(m.rows.every((r) => r.splitRow)).toBe(true);
    expect(m.entries().length).toBe(1);
  });

  test("itemLimit trims oldest rows first", () => {
    const m = new LogModel();
    m.itemLimit = 3;
    for (let loop = 0; loop < 5; loop++) {
      m.add(newEntry(`msg${loop}`));
    }
    expect(m.rows.length).toBe(3);
    expect(m.rows[0].line).toBe("[Info] msg2");
    expect(m.displayedRows.length).toBe(3);
  });

  test("pause buffers entries and applies them on unpause", () => {
    const m = new LogModel();
    m.paused = true;
    m.add(newEntry("while paused"));
    expect(m.rows.length).toBe(0);
    m.paused = false;
    expect(m.rows.length).toBe(1);
    expect(m.rows[0].line).toBe("[Info] while paused");
  });

  test("copyText prefixes each displayed row with the timestamp", () => {
    const m = new LogModel();
    m.add(newEntry("hello"));
    const text = m.copyText(() => "TS ");
    expect(text).toBe("TS [Info] hello");
  });
});

describe("csvExport", () => {
  test("11 columns, header first, ISO time, quoting via core/csv", () => {
    const entry = newEntry('has,comma and "quote"', "warn", { time: 0 });
    entry.callPath = ":Grid";
    const csv = entriesToCsv([entry]);
    const rows = parseCsvRows(csv.trim());
    expect(rows[0]).toEqual(CSV_HEADER);
    expect(rows[1].length).toBe(11);
    expect(rows[1][0]).toBe("1970-01-01T00:00:00.000Z");
    expect(rows[1][1]).toBe("Warn");
    expect(rows[1][6]).toBe(":Grid");
    // note: parseCsvRows drops doubled quotes on load (a pinned C# CSVLoad
    // quirk), so assert on the raw row text for the quoting itself
    expect(entryToCsvRow(entry)).toContain('"has,comma and ""quote"""');
  });

  test("source column composes file[line]:member", () => {
    const entry = newEntry("x", "info", {
      sourcePath: "c:/src/deep/Foo.cs",
      sourceLineNumber: 7,
      memberName: "Bar",
      time: 0,
    });
    expect(entryToCsvRow(entry)).toContain("Foo[7]:Bar");
  });
});
