"""TypeScript-oriented script management tools for Puerts-based Unity projects."""
from __future__ import annotations

import hashlib
import re
import shutil
import subprocess
from pathlib import Path
from typing import Annotated, Any, Literal

from fastmcp import Context

from registry import mcp_for_unity_tool
from tools import get_unity_instance_from_context
from tools.resource_tools import _resolve_project_root, _resolve_safe_path_from_uri

TS_EXTENSIONS = {".ts", ".tsx"}
TS_CONFIG_CANDIDATES = [
    "tsconfig.puerts.json",
    "tsconfig.unity.json",
    "tsconfig.json",
    "Assets/Puerts/tsconfig.json",
    "Assets/Puerts/TypeScripts/tsconfig.json",
    "Assets/TypeScripts/tsconfig.json",
]
TS_DIR_HINTS = [
    "Assets/TypeScripts",
    "Assets/Puerts",
    "Assets/Puerts/TypeScripts",
    "Assets/Scripts",
    "Assets",
]


class _ValidationError(Exception):
    """Internal exception to bubble structured validation issues."""


def _normalize_assets_relative(path: str) -> str:
    if not path:
        raise _ValidationError("path is required and must start with Assets/")
    normalized = path.replace("\\", "/").strip()
    if normalized.startswith("./"):
        normalized = normalized[2:]
    normalized = normalized.lstrip("/")
    parts = [p for p in normalized.split("/") if p not in ("", ".")]
    if not parts or parts[0].lower() != "assets":
        raise _ValidationError("path must live under Assets/")
    if any(part == ".." for part in parts):
        raise _ValidationError("path must not contain traversal segments")
    parts[0] = "Assets"
    return "/".join(parts)


def _ensure_ts_extension(path: Path):
    if path.suffix.lower() not in TS_EXTENSIONS:
        raise _ValidationError("TypeScript files must use .ts or .tsx extensions")


def _resolve_ts_path(project: Path, target: str, *, allow_create: bool = False) -> Path:
    if not target:
        raise _ValidationError("A TypeScript URI or Assets-relative path is required")
    resolved: Path | None = None
    if target.startswith(("unity://path/", "file://", "Assets/")):
        resolved = _resolve_safe_path_from_uri(target, project)
        if resolved is None:
            raise _ValidationError("Unable to resolve target path inside the project")
    else:
        rel = _normalize_assets_relative(target)
        resolved = (project / rel).resolve()
    try:
        resolved.relative_to(project / "Assets")
    except ValueError:
        raise _ValidationError("TypeScript files must reside under Assets/")
    _ensure_ts_extension(resolved)
    if not allow_create and not resolved.exists():
        raise FileNotFoundError(f"TypeScript file not found: {resolved}")
    return resolved


def _path_metadata(project: Path, path: Path) -> dict[str, Any]:
    rel = path.relative_to(project).as_posix()
    sha = hashlib.sha256(path.read_bytes()).hexdigest() if path.exists() else None
    data: dict[str, Any] = {
        "assetsPath": rel,
        "uri": f"unity://path/{rel}",
    }
    if sha:
        data.update({
            "sha256": sha,
            "lengthBytes": path.stat().st_size,
        })
    return data


def _strip_ansi(text: str) -> str:
    return re.sub(r"\x1B\[[0-?]*[ -/]*[@-~]", "", text)


def _detect_tsconfig(project: Path) -> Path | None:
    for candidate in TS_CONFIG_CANDIDATES:
        candidate_path = (project / candidate).resolve()
        if candidate_path.exists():
            return candidate_path
    return None


def _resolve_tsconfig(project: Path, tsconfig: str | None) -> Path | None:
    if tsconfig:
        candidate = Path(tsconfig)
        if not candidate.is_absolute():
            candidate = (project / tsconfig).resolve()
        if not candidate.exists():
            raise _ValidationError(f"tsconfig not found: {candidate}")
        return candidate
    return _detect_tsconfig(project)


def _tsc_command(tsc_path: str | None) -> list[str]:
    if tsc_path:
        return [tsc_path]
    resolved = shutil.which("tsc")
    if resolved:
        return [resolved]
    npx = shutil.which("npx")
    if npx:
        return [npx, "--yes", "tsc"]
    raise FileNotFoundError("tsc or npx not found on PATH; install TypeScript before validating")


def _parse_tsc_output(output: str, project: Path) -> list[dict[str, Any]]:
    diagnostics: list[dict[str, Any]] = []
    pattern = re.compile(
        r"^(?P<file>.+?)\((?P<line>\d+),(?P<col>\d+)\):\s+(?P<severity>error|warning)\s+(?P<code>TS\d+):\s+(?P<message>.+)$",
        re.IGNORECASE,
    )
    for raw_line in _strip_ansi(output).splitlines():
        line = raw_line.strip()
        if not line:
            continue
        match = pattern.match(line)
        if not match:
            continue
        file_path = Path(match.group("file")).resolve()
        try:
            rel = file_path.relative_to(project).as_posix()
        except ValueError:
            rel = file_path.as_posix()
        diagnostics.append({
            "file": rel,
            "line": int(match.group("line")),
            "column": int(match.group("col")),
            "severity": match.group("severity").lower(),
            "code": match.group("code"),
            "message": match.group("message").strip(),
        })
    return diagnostics


def _safe_project(ctx: Context, project_root: str | None) -> Path:
    return _resolve_project_root(ctx, project_root)


@mcp_for_unity_tool(description="Create a new TypeScript file at the given Assets-relative path.")
def create_ts_script(
    ctx: Context,
    path: Annotated[str, "Path under Assets/ to create the TypeScript file at, e.g., 'Assets/TypeScripts/ui.ts'"],
    contents: Annotated[str, "TypeScript source code to write"],
    overwrite: Annotated[bool, "Allow replacing an existing file"] = False,
    project_root: Annotated[str, "Optional project root override"] | None = None,
) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)
    ctx.info(f"Processing create_ts_script: {path} (unity_instance={unity_instance or 'default'})")
    try:
        project = _safe_project(ctx, project_root)
        target = _resolve_ts_path(project, path, allow_create=True)
        if target.exists() and not overwrite:
            return {"success": False, "code": "file_exists", "message": f"{target} already exists"}
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(contents or "", encoding="utf-8")
        data = _path_metadata(project, target)
        return {"success": True, "message": "TypeScript file created", "data": data}
    except _ValidationError as ve:
        return {"success": False, "message": str(ve)}
    except Exception as exc:
        return {"success": False, "message": f"create_ts_script error: {exc}"}


@mcp_for_unity_tool(description="Delete a TypeScript file identified by URI or Assets-relative path.")
def delete_ts_script(
    ctx: Context,
    uri: Annotated[str, "URI or Assets-relative path pointing to the TypeScript file"],
    project_root: Annotated[str, "Optional project root override"] | None = None,
) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)
    ctx.info(f"Processing delete_ts_script: {uri} (unity_instance={unity_instance or 'default'})")
    try:
        project = _safe_project(ctx, project_root)
        target = _resolve_ts_path(project, uri)
        metadata = _path_metadata(project, target)
        target.unlink(missing_ok=False)
        return {"success": True, "message": "TypeScript file deleted", "data": metadata}
    except FileNotFoundError:
        return {"success": False, "code": "not_found", "message": "TypeScript file not found"}
    except _ValidationError as ve:
        return {"success": False, "message": str(ve)}
    except Exception as exc:
        return {"success": False, "message": f"delete_ts_script error: {exc}"}


@mcp_for_unity_tool(description="Validate a TypeScript file or project using tsc and return diagnostics.")
def validate_ts_script(
    ctx: Context,
    uri: Annotated[str | None, "Optional URI or Assets path to focus validation on"] | None = None,
    tsconfig: Annotated[str | None, "Optional tsconfig path (relative to project or absolute)"] | None = None,
    strict: Annotated[bool, "Append --strict to tsc"] = False,
    incremental: Annotated[bool, "Append --incremental to tsc"] = False,
    include_diagnostics: Annotated[bool, "Include parsed diagnostics and raw output"] = True,
    project_root: Annotated[str, "Optional project root override"] | None = None,
    tsc_path: Annotated[str | None, "Custom path to the tsc executable"] | None = None,
) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)
    ctx.info(f"Processing validate_ts_script (unity_instance={unity_instance or 'default'})")
    try:
        project = _safe_project(ctx, project_root)
        tsconfig_path = _resolve_tsconfig(project, tsconfig)
        target_path = _resolve_ts_path(project, uri) if uri else None
        if not tsconfig_path and not target_path:
            raise _ValidationError("Provide a tsconfig or a specific TypeScript file to validate")
        cmd = _tsc_command(tsc_path)
        cmd += ["--pretty", "false", "--noEmit"]
        if strict:
            cmd.append("--strict")
        if incremental:
            cmd.append("--incremental")
        if tsconfig_path:
            cmd += ["--project", str(tsconfig_path)]
        if target_path and not tsconfig_path:
            cmd.append(str(target_path))
        proc = subprocess.run(
            cmd,
            cwd=project,
            capture_output=True,
            text=True,
            check=False,
        )
        output = (proc.stdout or "") + (proc.stderr or "")
        diagnostics = _parse_tsc_output(output, project) if include_diagnostics else []
        summary = {
            "warnings": sum(1 for d in diagnostics if d["severity"] == "warning"),
            "errors": sum(1 for d in diagnostics if d["severity"] == "error"),
        }
        data: dict[str, Any] = {
            "exitCode": proc.returncode,
            "summary": summary,
            "command": " ".join(cmd),
            "tsconfig": str(tsconfig_path) if tsconfig_path else None,
            "target": target_path.relative_to(project).as_posix() if target_path else None,
        }
        if include_diagnostics:
            data["diagnostics"] = diagnostics
            data["rawOutput"] = output
        success = proc.returncode == 0
        message = "TypeScript validation succeeded" if success else "TypeScript validation reported issues"
        return {"success": success, "message": message, "data": data}
    except FileNotFoundError as fnf:
        return {"success": False, "message": str(fnf)}
    except _ValidationError as ve:
        return {"success": False, "message": str(ve)}
    except Exception as exc:
        return {"success": False, "message": f"validate_ts_script error: {exc}"}


@mcp_for_unity_tool(description="Generic TypeScript file management (create/read/delete/write/append/rename).")
def manage_ts_script(
    ctx: Context,
    action: Annotated[Literal['create', 'read', 'delete', 'write', 'append', 'rename'], "Operation to perform"],
    path: Annotated[str, "Primary TypeScript URI or Assets path"],
    contents: Annotated[str, "Content for create/write/append"] | None = None,
    target_path: Annotated[str | None, "Destination path for rename"] | None = None,
    overwrite: Annotated[bool, "Allow overwrite for create/write/rename"] = False,
    create_if_missing: Annotated[bool, "Allow write to create new files"] = False,
    project_root: Annotated[str, "Optional project root override"] | None = None,
) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)
    ctx.info(f"Processing manage_ts_script: {action} (unity_instance={unity_instance or 'default'})")
    try:
        project = _safe_project(ctx, project_root)
        if action == 'create':
            return create_ts_script(ctx, path, contents or "", overwrite=overwrite, project_root=str(project))
        if action == 'delete':
            return delete_ts_script(ctx, path, project_root=str(project))
        primary = _resolve_ts_path(project, path, allow_create=action in {'write'} and create_if_missing)
        if action == 'read':
            text = primary.read_text(encoding="utf-8")
            data = _path_metadata(project, primary)
            data["contents"] = text
            return {"success": True, "data": data}
        if action == 'write':
            if not primary.exists() and not create_if_missing:
                raise FileNotFoundError("TypeScript file does not exist; enable create_if_missing to create new files")
            if primary.exists() and not overwrite and not create_if_missing:
                raise _ValidationError("Set overwrite=true to replace existing files")
            primary.parent.mkdir(parents=True, exist_ok=True)
            primary.write_text(contents or "", encoding="utf-8")
            data = _path_metadata(project, primary)
            return {"success": True, "message": "TypeScript file written", "data": data}
        if action == 'append':
            if not primary.exists():
                raise FileNotFoundError("TypeScript file does not exist for append")
            with primary.open("a", encoding="utf-8") as fh:
                fh.write(contents or "")
            data = _path_metadata(project, primary)
            return {"success": True, "message": "Content appended", "data": data}
        if action == 'rename':
            if not target_path:
                raise _ValidationError("target_path is required for rename")
            destination = _resolve_ts_path(project, target_path, allow_create=True)
            if destination.exists() and not overwrite:
                raise _ValidationError("Destination already exists; set overwrite=true to replace")
            destination.parent.mkdir(parents=True, exist_ok=True)
            primary.replace(destination)
            data = {
                "from": _path_metadata(project, primary)["assetsPath"],
                "to": _path_metadata(project, destination)["assetsPath"],
                "uri": f"unity://path/{destination.relative_to(project).as_posix()}",
            }
            return {"success": True, "message": "TypeScript file renamed", "data": data}
        raise _ValidationError(f"Unsupported manage_ts_script action: {action}")
    except FileNotFoundError as fnf:
        return {"success": False, "message": str(fnf)}
    except _ValidationError as ve:
        return {"success": False, "message": str(ve)}
    except Exception as exc:
        return {"success": False, "message": f"manage_ts_script error: {exc}"}


@mcp_for_unity_tool(description="Report capabilities for TypeScript script management and validation.")
def manage_ts_script_capabilities(
    ctx: Context,
    project_root: Annotated[str, "Optional project root override"] | None = None,
) -> dict[str, Any]:
    ctx.info("Processing manage_ts_script_capabilities")
    try:
        project = _safe_project(ctx, project_root)
        tsconfig_path = _detect_tsconfig(project)
        default_dirs = [d for d in TS_DIR_HINTS if (project / d).exists()]
        data = {
            "extensions": sorted(TS_EXTENSIONS),
            "actions": ['create', 'read', 'delete', 'write', 'append', 'rename'],
            "validation": {
                "tsconfig": str(tsconfig_path) if tsconfig_path else None,
                "requiresNode": True,
                "command": "tsc --pretty false --noEmit",
            },
            "defaultDirectories": default_dirs,
        }
        return {"success": True, "data": data}
    except Exception as exc:
        return {"success": False, "error": f"capabilities error: {exc}"}
