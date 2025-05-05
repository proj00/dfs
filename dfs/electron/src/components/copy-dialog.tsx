"use client"

import { useState, useEffect } from "react"
import { FolderOpen, ChevronRight, Copy } from "lucide-react"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "./ui/dialog"
import { Button } from "./ui/button"
import { Input } from "./ui/input"
import { Checkbox } from "./ui/checkbox"
import { Label } from "./ui/label"
import type { File, Folder } from "../lib/types"

interface CopyDialogProps {
    open: boolean
    onOpenChange: (open: boolean) => void
    item: File | Folder | null
    folders: Folder[]
    onCopy: (item: File | Folder, destinationFolderId: string | null, keepOriginalName: boolean) => Promise<void>
    existingNames?: string[] // Optional array of existing names in the destination to check for conflicts
}

export function CopyDialog({ open, onOpenChange, item, folders, onCopy, existingNames = [] }: CopyDialogProps) {
    const [selectedFolderId, setSelectedFolderId] = useState<string | null>(null)
    const [customPath, setCustomPath] = useState("")
    const [keepOriginalName, setKeepOriginalName] = useState(false)
    const [isCopying, setIsCopying] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [nameConflict, setNameConflict] = useState(false)

    // Reset state when dialog opens
    useEffect(() => {
        if (open) {
            setSelectedFolderId(null)
            setCustomPath("")
            setKeepOriginalName(false)
            setError(null)
            setNameConflict(false)
        }
    }, [open])

    // Get root folders (folders with empty parentId array)
    const rootFolders = folders.filter((folder) => folder.parentId.length === 0)

    // Get folder name by ID
    const getFolderName = (folderId: string | null) => {
        if (folderId === null) return "My Drive"
        return folders.find((folder) => folder.id === folderId)?.name || "Unknown Folder"
    }

    // Check for name conflicts in the destination folder
    useEffect(() => {
        if (!item) return

        // Get names in the destination folder
        const destinationNames = existingNames.filter((name) => name !== item.name) // Exclude the current item's name

        // Check if there's a conflict
        const hasConflict = destinationNames.includes(item.name)
        setNameConflict(hasConflict)
    }, [selectedFolderId, item, existingNames])

    // Handle copy action
    const handleCopy = async () => {
        if (!item) return

        // Use custom path if provided, otherwise use selected folder
        const destinationId = customPath ? null : selectedFolderId

        setIsCopying(true)
        setError(null)

        try {
            await onCopy(item, destinationId, keepOriginalName)
            onOpenChange(false)
        } catch (err) {
            console.error("Copy failed:", err)
            setError("Failed to copy item. Please try again.")
        } finally {
            setIsCopying(false)
        }
    }

    // Get current location of the item
    const getCurrentLocation = () => {
        if (!item) return ""

        // If the item has no parents, it's in the root
        if (item.parentId.length === 0) return "My Drive"

        // Otherwise, get the name of the first parent folder
        const parentId = item.parentId[0]
        return getFolderName(parentId)
    }

    const currentLocation = getCurrentLocation()
    const itemType = item ? (item.hasOwnProperty("size") ? "file" : "folder") : ""

    return (
        <Dialog open={open} onOpenChange={(open) => !isCopying && onOpenChange(open)}>
            <DialogContent className="sm:max-w-[500px]">
                <DialogHeader>
                    <DialogTitle className="flex items-center">
                        <Copy className="h-5 w-5 mr-2" />
                        Copy {itemType}
                    </DialogTitle>
                    <DialogDescription>
                        Select a destination folder to copy this {itemType}. A copy will be created in the selected location.
                    </DialogDescription>
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

                <div className="space-y-4 py-2">
                    <div className="text-sm">
                        <span className="font-medium">Current location:</span> {currentLocation}
                    </div>

                    <div className="space-y-2">
                        <div className="font-medium text-sm">Select destination folder:</div>
                        <div className="border rounded-md h-60 overflow-y-auto p-2">
                            <div
                                className={`flex items-center p-2 rounded-md cursor-pointer ${selectedFolderId === null ? "bg-muted" : "hover:bg-muted/50"
                                    }`}
                                onClick={() => setSelectedFolderId(null)}
                            >
                                <FolderOpen className="h-5 w-5 mr-2 text-blue-500" />
                                <span>My Drive (root)</span>
                            </div>

                            {rootFolders.map((folder) => (
                                <FolderItem
                                    key={folder.id}
                                    folder={folder}
                                    folders={folders}
                                    selectedFolderId={selectedFolderId}
                                    setSelectedFolderId={setSelectedFolderId}
                                    level={0}
                                    currentItemId={item?.id || ""}
                                />
                            ))}
                        </div>
                    </div>

                    <div className="space-y-2">
                        <div className="font-medium text-sm">Or enter a custom path:</div>
                        <Input
                            placeholder="/path/to/destination"
                            value={customPath}
                            onChange={(e) => setCustomPath(e.target.value)}
                            disabled={isCopying}
                        />
                        <p className="text-xs text-muted-foreground">
                            Enter a path like "/Work Documents/Projects" to copy to a specific location.
                        </p>
                    </div>

                    {nameConflict && (
                        <div className="flex items-start space-x-2 p-3 bg-amber-50 rounded-md border border-amber-200">
                            <div className="flex h-5 w-5 shrink-0 items-center justify-center text-amber-500">
                                <svg
                                    xmlns="http://www.w3.org/2000/svg"
                                    width="16"
                                    height="16"
                                    viewBox="0 0 24 24"
                                    fill="none"
                                    stroke="currentColor"
                                    strokeWidth="2"
                                    strokeLinecap="round"
                                    strokeLinejoin="round"
                                >
                                    <path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z" />
                                    <path d="M12 9v4" />
                                    <path d="M12 17h.01" />
                                </svg>
                            </div>
                            <div className="space-y-1">
                                <p className="text-sm font-medium text-amber-800">Name conflict detected</p>
                                <p className="text-xs text-amber-700">
                                    An item with the same name already exists in the destination. Choose an option below:
                                </p>
                                <div className="flex items-center space-x-2 mt-2">
                                    <Checkbox
                                        id="keep-original"
                                        checked={keepOriginalName}
                                        onCheckedChange={(checked) => setKeepOriginalName(checked as boolean)}
                                    />
                                    <Label htmlFor="keep-original" className="text-sm">
                                        Keep both (add a number to the copy)
                                    </Label>
                                </div>
                            </div>
                        </div>
                    )}
                </div>

                <DialogFooter>
                    <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isCopying}>
                        Cancel
                    </Button>
                    <Button
                        onClick={handleCopy}
                        disabled={isCopying || (!selectedFolderId && !customPath)}
                        className="flex items-center"
                    >
                        {isCopying ? (
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
                                Copying...
                            </span>
                        ) : (
                            <>
                                <Copy className="h-4 w-4 mr-2" />
                                Copy
                            </>
                        )}
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    )
}

// Recursive folder item component
interface FolderItemProps {
    folder: Folder
    folders: Folder[]
    selectedFolderId: string | null
    setSelectedFolderId: (id: string | null) => void
    level: number
    currentItemId: string
}

function FolderItem({ folder, folders, selectedFolderId, setSelectedFolderId, level, currentItemId }: FolderItemProps) {
    const [expanded, setExpanded] = useState(false)

    // Get child folders (folders that have this folder's ID in their parentId array)
    const childFolders = folders.filter((f) => f.parentId.includes(folder.id))

    // Check if this folder is the current item (can't copy to itself)
    const isSameAsCurrentItem = folder.id === currentItemId

    return (
        <div className="ml-2">
            <div
                className={`flex items-center p-2 rounded-md cursor-pointer ${isSameAsCurrentItem
                        ? "opacity-50 cursor-not-allowed"
                        : selectedFolderId === folder.id
                            ? "bg-muted"
                            : "hover:bg-muted/50"
                    }`}
                onClick={() => !isSameAsCurrentItem && setSelectedFolderId(folder.id)}
            >
                {folder.hasChildren && (
                    <ChevronRight
                        className={`h-4 w-4 mr-1 transition-transform ${expanded ? "rotate-90" : ""}`}
                        onClick={(e) => {
                            e.stopPropagation()
                            setExpanded(!expanded)
                        }}
                    />
                )}
                {!folder.hasChildren && <div className="w-5" />}
                <FolderOpen className="h-5 w-5 mr-2 text-blue-500" />
                <span className={isSameAsCurrentItem ? "text-muted-foreground" : ""}>
                    {folder.name}
                    {isSameAsCurrentItem && " (current)"}
                </span>
            </div>

            {expanded && childFolders.length > 0 && (
                <div className="ml-4">
                    {childFolders.map((childFolder) => (
                        <FolderItem
                            key={childFolder.id}
                            folder={childFolder}
                            folders={folders}
                            selectedFolderId={selectedFolderId}
                            setSelectedFolderId={setSelectedFolderId}
                            level={level + 1}
                            currentItemId={currentItemId}
                        />
                    ))}
                </div>
            )}
        </div>
    )
}
