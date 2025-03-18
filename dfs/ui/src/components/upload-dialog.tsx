import type React from "react"

import { Button } from "@/components/ui/button"
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Upload, FolderUp } from "lucide-react"
import { useState, useRef } from "react"

interface UploadDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}

export function UploadDialog({ open, onOpenChange }: UploadDialogProps) {
  const [isDragging, setIsDragging] = useState(false)
  const fileInputRef = useRef<HTMLInputElement>(null)

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault()
    setIsDragging(true)
  }

  const handleDragLeave = () => {
    setIsDragging(false)
  }

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault()
    setIsDragging(false)

    // In a real app, you would handle the dropped files here
    const files = Array.from(e.dataTransfer.files)
    console.log("Dropped files:", files)

    // Close the dialog after files are dropped
    onOpenChange(false)
  }

  const handleFileSelect = () => {
    fileInputRef.current?.click()
  }

  const handleFileInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    // In a real app, you would handle the selected files here
    const files = e.target.files ? Array.from(e.target.files) : []
    console.log("Selected files:", files)

    // Close the dialog after files are selected
    if (files.length > 0) {
      onOpenChange(false)
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Upload files</DialogTitle>
          <DialogDescription>Drag and drop files or folders to upload</DialogDescription>
        </DialogHeader>
        <div
          className={`mt-4 border-2 border-dashed rounded-lg p-10 text-center ${
            isDragging ? "border-primary bg-primary/10" : "border-muted-foreground/25"
          }`}
          onDragOver={handleDragOver}
          onDragLeave={handleDragLeave}
          onDrop={handleDrop}
        >
          <div className="flex flex-col items-center gap-4">
            <div className="rounded-full bg-muted p-4">
              <Upload className="h-8 w-8 text-muted-foreground" />
            </div>
            <div>
              <p className="text-sm font-medium">Drag files or folders here</p>
              <p className="text-xs text-muted-foreground mt-1">Or click the button below to browse</p>
            </div>
            <Button onClick={handleFileSelect} className="mt-2">
              <FolderUp className="mr-2 h-4 w-4" />
              Choose files
            </Button>
            <input type="file" ref={fileInputRef} className="hidden" multiple onChange={handleFileInputChange} />
          </div>
        </div>
      </DialogContent>
    </Dialog>
  )
}

