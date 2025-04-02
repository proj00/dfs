"use client";

import type React from "react";
import { Button } from "./ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "./ui/dialog";
import { FolderUp } from "lucide-react";
import { useRef } from "react";

interface UploadDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function UploadDialog({ open, onOpenChange }: UploadDialogProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleFileSelect = () => {
    fileInputRef.current?.click();
  };

  const handleFileInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    // In a real app, you would handle the selected files here
    const files = e.target.files ? Array.from(e.target.files) : [];
    console.log("Selected files:", files);

    // Close the dialog after files are selected
    if (files.length > 0) {
      onOpenChange(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Upload files</DialogTitle>
          <DialogDescription>
            Select files to upload to your drive
          </DialogDescription>
        </DialogHeader>
        <div className="mt-4 border rounded-lg p-10 text-center">
          <div className="flex flex-col items-center gap-4">
            <div className="rounded-full bg-muted p-4">
              <FolderUp className="h-8 w-8 text-muted-foreground" />
            </div>
            <div>
              <p className="text-sm font-medium">Select files to upload</p>
              <p className="text-xs text-muted-foreground mt-1">
                Choose files from your device
              </p>
            </div>
            <Button onClick={handleFileSelect} className="mt-2">
              <FolderUp className="mr-2 h-4 w-4" />
              Choose files
            </Button>
            <input
              type="file"
              ref={fileInputRef}
              className="hidden"
              multiple
              onChange={handleFileInputChange}
            />
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
