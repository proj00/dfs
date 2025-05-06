"use client"

import { ArrowRight } from "lucide-react"
import type { File, Folder } from "../lib/types"
import { FileOperationDialog } from "./file-operation-dialog"

interface MoveDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  item: File | Folder | null
  folders: Folder[]
  onMove: (item: File | Folder, destinationFolderId: string | null) => Promise<void>
}

export function MoveDialog({ open, onOpenChange, item, folders, onMove }: MoveDialogProps) {
  const isValidDestination = (folder: Folder, currentItemId: string): boolean => {
    if (folder.id === currentItemId) return false

    if (item && item.hasOwnProperty("hasChildren")) {
      const isDescendant = (folderId: string, itemId: string): boolean => {
        const folder = folders.find((f) => f.id === folderId)
        if (!folder) return false

        if (folder.parentId.includes(itemId)) return true

        return folder.parentId.some((parentId) => isDescendant(parentId, itemId))
      }

      return !isDescendant(folder.id, currentItemId)
    }

    return true
  }

  const itemType = item ? (item.hasOwnProperty("size") ? "file" : "folder") : ""

  return (
    <FileOperationDialog
      open={open}
      onOpenChange={onOpenChange}
      item={item}
      folders={folders}
      onAction={onMove}
      title={
        <div className="flex items-center">
          <ArrowRight className="h-5 w-5 mr-2" />
          Move {itemType}
        </div>
      }
      description={`Select a destination folder or enter a custom path to move this ${itemType}.`}
      actionButtonText="Move"
      actionButtonIcon={<ArrowRight className="h-4 w-4 mr-2" />}
      isValidDestination={isValidDestination}
    />
  )
}
