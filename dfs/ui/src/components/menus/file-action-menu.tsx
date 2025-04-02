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
import type { File } from "../../lib/types";
import { copyToClipboard } from "@/lib/utils";

interface FileActionMenuProps {
  file: File;
  onOpenClick?: (file: File, e: React.MouseEvent) => Promise<void> | void;
  onRenameClick?: (file: File, e: React.MouseEvent) => Promise<void> | void;
  onMoveClick?: (file: File, e: React.MouseEvent) => Promise<void> | void;
  onDeleteClick?: (file: File, e: React.MouseEvent) => Promise<void> | void;
}

export function FileActionMenu({
  file,
  onOpenClick,
  onRenameClick,
  onMoveClick,
  onDeleteClick,
}: FileActionMenuProps) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon" className="h-8 w-8">
          <MoreHorizontal className="h-4 w-4" />
          <span className="sr-only">File actions</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem
          onClick={async (e) => {
            e.stopPropagation();
            if (onOpenClick) await onOpenClick(file, e);
          }}
        >
          Open
        </DropdownMenuItem>
        <DropdownMenuItem
          onClick={async (e) => {
            e.stopPropagation();
            await copyToClipboard(file.containerGuid);
            console.log(`Copied file GUID: ${file.containerGuid}`);
          }}
        >
          Copy container GUID
        </DropdownMenuItem>
        <DropdownMenuItem
          onClick={async (e) => {
            e.stopPropagation();
            if (onRenameClick) await onRenameClick(file, e);
          }}
        >
          Rename
        </DropdownMenuItem>
        <DropdownMenuItem
          onClick={async (e) => {
            e.stopPropagation();
            if (onMoveClick) await onMoveClick(file, e);
          }}
        >
          Move to
        </DropdownMenuItem>
        <DropdownMenuItem
          className="text-destructive"
          onClick={async (e) => {
            e.stopPropagation();
            if (onDeleteClick) await onDeleteClick(file, e);
          }}
        >
          Delete
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
