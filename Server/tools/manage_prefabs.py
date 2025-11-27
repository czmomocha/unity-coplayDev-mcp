from typing import Annotated, Any, Literal

from fastmcp import Context
from registry import mcp_for_unity_tool
from tools import get_unity_instance_from_context, send_with_unity_instance
from unity_connection import send_command_with_retry

SUPPORTED_PREFAB_ACTIONS = (
    "open_stage",
    "close_stage",
    "save_open_stage",
    "create_from_gameobject",
)


def _normalize_prefab_path(path: str | None) -> str:
    if not path:
        raise ValueError("prefab_path is required for this action")
    normalized = path.replace("\\", "/").strip()
    if normalized.startswith("./"):
        normalized = normalized[2:]
    if not normalized.lower().startswith("assets/"):
        raise ValueError("prefab_path must be under Assets/")
    return normalized


def _validate_mode(mode: str | None) -> str | None:
    if not mode:
        return None
    if mode.lower() != "inisolation":
        raise ValueError("Only 'InIsolation' prefab stage mode is supported")
    return "InIsolation"


def _bool_or_none(value: bool | None) -> bool | None:
    if value is None:
        return None
    return bool(value)


@mcp_for_unity_tool(
    description="Manage Unity Prefab stages (open, save, close, create from scene objects)."
)
def manage_prefabs(
    ctx: Context,
    action: Annotated[Literal[
        "open_stage",
        "close_stage",
        "save_open_stage",
        "create_from_gameobject",
    ], "Prefab Stage action to perform"],
    prefab_path: Annotated[str | None,
                           "Prefab asset path under Assets/, e.g., Assets/Prefabs/My.prefab"] = None,
    mode: Annotated[str | None,
                    "PrefabStage mode (currently only 'InIsolation' is supported)"] = None,
    save_before_close: Annotated[bool | None,
                                 "Whether close_stage should save pending changes before exit"] = None,
    target: Annotated[str | None,
                      "Scene or Prefab Stage GameObject name for create_from_gameobject"] = None,
    allow_overwrite: Annotated[bool | None,
                               "Allow replacing an existing prefab when creating from GameObject"] = None,
    search_inactive: Annotated[bool | None,
                               "Include inactive transforms while resolving target"] = None,
) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)
    try:
        action_lower = action.lower()
        params: dict[str, Any] = {"action": action_lower}

        if action_lower in ("open_stage", "create_from_gameobject"):
            params["prefabPath"] = _normalize_prefab_path(prefab_path)
        elif prefab_path:
            params["prefabPath"] = _normalize_prefab_path(prefab_path)

        validated_mode = _validate_mode(mode)
        if validated_mode:
            params["mode"] = validated_mode

        if action_lower == "close_stage" and save_before_close is not None:
            params["saveBeforeClose"] = bool(save_before_close)

        if action_lower == "create_from_gameobject":
            if not target:
                raise ValueError("'target' is required when action=create_from_gameobject")
            params["target"] = target
            allow_flag = _bool_or_none(allow_overwrite)
            if allow_flag is not None:
                params["allowOverwrite"] = allow_flag
            include_inactive = _bool_or_none(search_inactive)
            if include_inactive is not None:
                params["searchInactive"] = include_inactive

        response = send_with_unity_instance(
            send_command_with_retry,
            unity_instance,
            "manage_prefabs",
            params,
        )

        if isinstance(response, dict) and response.get("success"):
            return {
                "success": True,
                "message": response.get("message", "Prefab operation successful."),
                "data": response.get("data"),
            }
        return response if isinstance(response, dict) else {"success": False, "message": str(response)}
    except ValueError as validation_error:
        return {"success": False, "message": str(validation_error)}
    except Exception as exc:
        return {"success": False, "message": f"Python error managing prefabs: {exc}"}
