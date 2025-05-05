"use client"

import { useState, useEffect } from "react"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "./ui/dialog"
import { Button } from "./ui/button"
import { Input } from "./ui/input"
import { Label } from "./ui/label"
import type { File, Folder } from "../lib/types"

interface RenameDialogProps {
    open: boolean
    onOpenChange: (open: boolean) => void
    item: File | Folder | null
    onRename: (item: File | Folder, newName: string) => Promise<void>
    existingNames?: string[] // Optional array of existing names to check for duplicates
}

export function RenameDialog({ open, onOpenChange, item, onRename, existingNames = [] }: RenameDialogProps) {
    const [newName, setNewName] = useState("")
    const [isRenaming, setIsRenaming] = useState(false)
    const [error, setError] = useState<string | null>(null)

    // Reset state and set initial name when dialog opens
    useEffect(() => {
        if (open && item) {
            setNewName(item.name)
            setError(null)
        }
    }, [open, item])

    // Get file extension if it's a file
    const getFileExtension = (fileName: string): string => {
        const parts = fileName.split(".")
        return parts.length > 1 ? `.${parts[parts.length - 1]}` : ""
    }

    // Get file name without extension
    const getFileNameWithoutExtension = (fileName: string): string => {
        const extension = getFileExtension(fileName)
        if (!extension) return fileName
        return fileName.slice(0, fileName.length - extension.length)
    }

    // Handle rename action
    const handleRename = async () => {
        if (!item) return

        // Validate new name
        if (!newName.trim()) {
            setError("Name cannot be empty")
            return
        }

        // For files, make sure we preserve the extension
        let finalName = newName
        if (item.hasOwnProperty("size")) {
            const originalExtension = getFileExtension(item.name)
            const newExtension = getFileExtension(newName)

            // If the user removed the extension, add it back
            if (originalExtension && !newExtension) {
                finalName = `${newName}${originalExtension}`
            }
        }

        // Check for duplicate names
        if (existingNames.includes(finalName) && finalName !== item.name) {
            setError(`An item named "${finalName}" already exists in this location`)
            return
        }

        setIsRenaming(true)
        setError(null)

        try {
            await onRename(item, finalName)
            onOpenChange(false)
        } catch (err) {
            console.error("Rename failed:", err)
            setError("Failed to rename item. Please try again.")
        } finally {
            setIsRenaming(false)
        }
    }

    // Determine if it's a file or folder
    const itemType = item ? (item.hasOwnProperty("size") ? "file" : "folder") : ""

    // For files, we want to allow editing just the name part, not the extension
    const nameWithoutExtension = item && itemType === "file" ? getFileNameWithoutExtension(item.name) : ""
    const extension = item && itemType === "file" ? getFileExtension(item.name) : ""

    return (
        <Dialog open={open} onOpenChange={(open) => !isRenaming && onOpenChange(open)}>
            <DialogContent className="sm:max-w-[425px]">
                <DialogHeader>
                    <DialogTitle>Rename {itemType}</DialogTitle>
                    <DialogDescription>Enter a new name for this {itemType}.</DialogDescription>
                </DialogHeader>

                {error && (
                    <div className="bg-red-50 text-red-700 p-3 rounded-md mb-4 flex items-center">
                        <svg xmlns="http://www.w3.org/2000/svg" className="h-5 w-5 mr-2" viewBox="0 0 20 20" fill="currentColor">
                            <path
                                fillRule="evenodd"
                                d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z"
                                clipRule="evenodd"
                            />
                        </svg>
                        {error}
                    </div>
                )}

                <div className="grid gap-4 py-4">
                    <div className="grid grid-cols-4 items-center gap-4">
                        <Label htmlFor="name" className="text-right">
                            Name
                        </Label>
                        <div className="col-span-3 flex items-center">
                            <Input
                                id="name"
                                value={itemType === "file" ? nameWithoutExtension : newName}
                                onChange={(e) => {
                                    if (itemType === "file") {
                                        setNewName(e.target.value + extension)
                                    } else {
                                        setNewName(e.target.value)
                                    }
                                }}
                                className={itemType === "file" && extension ? "rounded-r-none" : ""}
                                autoFocus
                                onKeyDown={(e) => {
                                    if (e.key === "Enter") {
                                        handleRename()
                                    }
                                }}
                            />
                            {itemType === "file" && extension && (
                                <div className="bg-muted px-3 py-2 border border-l-0 rounded-r-md text-muted-foreground">
                                    {extension}
                                </div>
                            )}
                        </div>
                    </div>
                </div>

                <DialogFooter>
                    <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isRenaming}>
                        Cancel
                    </Button>
                    <Button onClick={handleRename} disabled={isRenaming || !newName.trim() || newName === item?.name}>
                        {isRenaming ? (
                            <span className="flex items-center">
                                <svg
                                    className="animate-spin -ml-1 mr-2 h-4 w-4 text-white"
                                    xmlns="http://www.w3.org/2000/svg"
                                    fill="none"
                                    viewBox="0 0 24 24"
                                >
                                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                                    <path
                                        className="opacity-75"
                                        fill="currentColor"
                                        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                                    ></path>
                                </svg>
                                Renaming...
                            </span>
                        ) : (
                            "Rename"
                        )}
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    )
}
