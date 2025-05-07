"use client"

import { useState } from "react"
import { Copy } from "lucide-react"
import type { File, Folder } from "../lib/types"
import { FileOperationDialog } from "./file-operation-dialog"
import { Checkbox } from "./ui/checkbox"

interface CopyDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  item: File | Folder | null
  folders: Folder[]
  onCopy: (item: File | Folder, destinationFolderId: string | null, keepOriginalName: boolean) => Promise<boolean>
  existingNames?: string[] 
}

export function CopyDialog({ open, onOpenChange, item, folders, onCopy }: CopyDialogProps) {
  const [keepOriginalName, setKeepOriginalName] = useState(true)

  const itemType = item ? (item.hasOwnProperty("size") ? "file" : "folder") : ""

  const extraContent = (
    <div className="flex items-center space-x-2 pt-2">
      <Checkbox
        id="keep-original-name"
        checked={keepOriginalName}
        onCheckedChange={(checked) => setKeepOriginalName(checked as boolean)}
      />
      <label
        htmlFor="keep-original-name"
        className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70"
      >
        Keep original name
      </label>
    </div>
  )

  return (
    <FileOperationDialog
      open={open}
      onOpenChange={onOpenChange}
      item={item}
      folders={folders}
      onAction={onCopy}
      title={
        <div className="flex items-center">
          <Copy className="h-5 w-5 mr-2" />
          Copy {itemType}
        </div>
      }
      description={`Select a destination folder or enter a custom path to copy this ${itemType}.`}
      actionButtonText="Copy"
      actionButtonIcon={<Copy className="h-4 w-4 mr-2" />}
      extraContent={extraContent}
      extraActionParams={[keepOriginalName]}
    />
  )
}
