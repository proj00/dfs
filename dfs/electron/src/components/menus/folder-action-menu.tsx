"use client";

import type React from "react";

import { MoreHorizontal } from "lucide-react";
import { Button } from "../ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "../ui/dropdown-menu";
import type { Folder } from "../../lib/types";
import log from "electron-log/renderer";

interface FolderActionMenuProps {
  readonly folder: Folder;
  readonly onRenameClick?: (
    folder: Folder,
    e: React.MouseEvent,
  ) => Promise<void> | void;
  readonly onCopyClick?: (
    folder: Folder,
    e: React.MouseEvent,
  ) => Promise<void> | void;
  readonly onMoveClick?: (
    folder: Folder,
    e: React.MouseEvent,
  ) => Promise<void> | void;
  readonly onDeleteClick?: (
    folder: Folder,
    e: React.MouseEvent,
  ) => Promise<void> | void;
}

export function FolderActionMenu({
  folder,
  onRenameClick,
  onMoveClick,
  onCopyClick,
  onDeleteClick,
}: FolderActionMenuProps) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon" className="h-8 w-8">
          <MoreHorizontal className="h-4 w-4" />
          <span className="sr-only">Folder actions</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem
          onClick={async (e) => {
            e.stopPropagation();
            window.electronAPI.writeClipboard(folder.containerGuid);
            log.info(`Copied folder GUID: ${folder.containerGuid}`);
          }}
        >
          Copy container GUID
        </DropdownMenuItem>
        <DropdownMenuItem
          onClick={async (e) => {
            e.stopPropagation();
            if (onRenameClick) await onRenameClick(folder, e);
          }}
        >
          Rename
        </DropdownMenuItem>
        <DropdownMenuItem
          onClick={async (e) => {
            e.stopPropagation();
            if (onMoveClick) await onMoveClick(folder, e);
          }}
        >
          Move to
        </DropdownMenuItem>
        <DropdownMenuItem
          onClick={async (e) => {
            e.stopPropagation();
            if (onCopyClick) await onCopyClick(folder, e);
          }}
        >
          Make a copy
        </DropdownMenuItem>
        <DropdownMenuItem
          className="text-destructive"
          onClick={async (e) => {
            e.stopPropagation();
            if (onDeleteClick) await onDeleteClick(folder, e);
          }}
        >
          Delete
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
