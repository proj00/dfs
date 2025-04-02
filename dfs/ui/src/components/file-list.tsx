"use client";

import { useState, useEffect } from "react";
import { ChevronLeft, MoreHorizontal } from "lucide-react";
import { Button } from "./ui/button";
import { DownloadToast } from "./ui/download-toast";
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuTrigger,
} from "./ui/dropdown-menu";
import type { File, Folder } from "../lib/types";
import { formatFileSize, formatDate } from "../lib/utils";

interface FileListProps {
    files: File[];
    folders: Folder[];
    currentFolder: string | null;
    currentFolderName: string;
    navigateToFolder: (folderId: string | null) => void;
    navigateToParent: () => void;
}

interface DownloadingFile {
    id: string;
    name: string;
    sizeMB: number;
    progress: number;
}

export function FileList({
    files,
    folders,
    currentFolder,
    currentFolderName,
    navigateToFolder,
    navigateToParent,
}: FileListProps) {
    const [downloadingFiles, setDownloadingFiles] = useState<DownloadingFile[]>([]);

    const handleDownload = (file: File) => {
        if (!downloadingFiles.find((f) => f.id === file.id)) {
            setDownloadingFiles((prev) => [
                ...prev,
                {
                    id: file.id,
                    name: file.name,
                    sizeMB: file.size / (1024 * 1024),
                    progress: 0,
                },
            ]);
        }
    };

    const handleCloseToast = (fileId: string) => {
        setDownloadingFiles((prev) => prev.filter((f) => f.id !== fileId));
    };

    useEffect(() => {
        const interval = setInterval(() => {
            setDownloadingFiles((prev) =>
                prev
                    .map((file) => {
                        const fakeSpeed = 0.2;
                        const added = (fakeSpeed / file.sizeMB) * 100;
                        const nextProgress = file.progress + added;
                        return {
                            ...file,
                            progress: nextProgress >= 100 ? 100 : nextProgress,
                        };
                    })
                    .filter((f) => f.progress < 100)
            );
        }, 1000);

        return () => clearInterval(interval);
    }, []);

    return (
        <div className="space-y-4">
            <div className="flex items-center">
                {currentFolder && (
                    <Button variant="ghost" size="sm" onClick={navigateToParent} className="mr-2">
                        <ChevronLeft className="h-4 w-4 mr-1" />
                        Back
                    </Button>
                )}
                <h2 className="text-xl font-semibold">{currentFolderName}</h2>
            </div>

            <div className="rounded-lg border">
                <div className="grid grid-cols-12 gap-4 p-3 text-sm font-medium text-muted-foreground border-b">
                    <div className="col-span-6">Name</div>
                    <div className="col-span-2">Owner</div>
                    <div className="col-span-3">Last modified</div>
                    <div className="col-span-1">Size</div>
                </div>

                {folders.length === 0 && files.length === 0 && (
                    <div className="p-8 text-center">
                        <div className="text-muted-foreground mb-4">
                            <svg
                                xmlns="http://www.w3.org/2000/svg"
                                width="48"
                                height="48"
                                viewBox="0 0 24 24"
                                fill="none"
                                stroke="currentColor"
                                strokeWidth="1"
                                strokeLinecap="round"
                                strokeLinejoin="round"
                                className="mx-auto mb-2"
                            >
                                <path d="M4 20h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.93a2 2 0 0 1-1.66-.9l-.82-1.2A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13c0 1.1.9 2 2 2Z" />
                            </svg>
                            <p>This folder is empty</p>
                        </div>
                        <p className="text-sm text-muted-foreground">
                            Files and folders you add to this location will appear here
                        </p>
                    </div>
                )}

                {folders.map((folder) => (
                    <div
                        key={folder.id}
                        className="grid grid-cols-12 gap-4 p-3 items-center hover:bg-muted/50 cursor-pointer group"
                        onClick={() => navigateToFolder(folder.id)}
                    >
                        <div className="col-span-6 flex items-center">
                            <div className="h-10 w-10 flex items-center justify-center text-blue-500 mr-3">
                                <svg
                                    xmlns="http://www.w3.org/2000/svg"
                                    width="24"
                                    height="24"
                                    viewBox="0 0 24 24"
                                    fill="none"
                                    stroke="currentColor"
                                    strokeWidth="2"
                                    strokeLinecap="round"
                                    strokeLinejoin="round"
                                    className="h-6 w-6"
                                >
                                    <path d="M4 20h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.93a2 2 0 0 1-1.66-.9l-.82-1.2A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13c0 1.1.9 2 2 2Z" />
                                </svg>
                            </div>
                            <span className="truncate">{folder.name}</span>
                        </div>
                        <div className="col-span-2">Me</div>
                        <div className="col-span-3">{formatDate(folder.modifiedAt)}</div>
                        <div className="col-span-1 flex items-center justify-between">
                            <span>—</span>
                            <div className="opacity-0 group-hover:opacity-100">
                                <DropdownMenu>
                                    <DropdownMenuTrigger asChild>
                                        <Button variant="ghost" size="icon" className="h-8 w-8">
                                            <MoreHorizontal className="h-4 w-4" />
                                            <span className="sr-only">More</span>
                                        </Button>
                                    </DropdownMenuTrigger>
                                    <DropdownMenuContent align="end">
                                        <DropdownMenuItem
                                            onClick={(e) => {
                                                e.stopPropagation();
                                                navigator.clipboard.writeText(folder.id);
                                                console.log(`Copied folder GUID: ${folder.id}`);
                                            }}
                                        >
                                            Copy container GUID
                                        </DropdownMenuItem>
                                        <DropdownMenuItem>Rename</DropdownMenuItem>
                                        <DropdownMenuItem>Move to</DropdownMenuItem>
                                        <DropdownMenuItem className="text-destructive">
                                            Delete
                                        </DropdownMenuItem>
                                    </DropdownMenuContent>
                                </DropdownMenu>
                            </div>
                        </div>
                    </div>
                ))}

                {files.map((file) => (
                    <div
                        key={file.id}
                        className="grid grid-cols-12 gap-4 p-3 items-center hover:bg-muted/50 cursor-pointer group"
                    >
                        <div className="col-span-6 flex items-center">
                            <div className="h-10 w-10 flex items-center justify-center mr-3">
                                <svg
                                    xmlns="http://www.w3.org/2000/svg"
                                    width="24"
                                    height="24"
                                    viewBox="0 0 24 24"
                                    fill="none"
                                    stroke="currentColor"
                                    strokeWidth="2"
                                    strokeLinecap="round"
                                    strokeLinejoin="round"
                                    className="h-6 w-6 text-gray-500"
                                >
                                    <path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5L14.5 2z" />
                                    <polyline points="14 2 14 8 20 8" />
                                </svg>
                            </div>
                            <span className="truncate">{file.name}</span>
                        </div>
                        <div className="col-span-2">Me</div>
                        <div className="col-span-3">{formatDate(file.modifiedAt)}</div>
                        <div className="col-span-1 flex items-center justify-between">
                            <span>{formatFileSize(file.size)}</span>
                            <div className="opacity-0 group-hover:opacity-100">
                                <DropdownMenu>
                                    <DropdownMenuTrigger asChild>
                                        <Button variant="ghost" size="icon" className="h-8 w-8">
                                            <MoreHorizontal className="h-4 w-4" />
                                            <span className="sr-only">More</span>
                                        </Button>
                                    </DropdownMenuTrigger>
                                    <DropdownMenuContent align="end">
                                        <DropdownMenuItem>Open</DropdownMenuItem>
                                        <DropdownMenuItem onClick={() => handleDownload(file)}>
                                            Download
                                        </DropdownMenuItem>
                                        <DropdownMenuItem
                                            onClick={(e) => {
                                                e.stopPropagation();
                                                navigator.clipboard.writeText(file.id);
                                                console.log(`Copied file GUID: ${file.id}`);
                                            }}
                                        >
                                            Copy container GUID
                                        </DropdownMenuItem>
                                        <DropdownMenuItem>Rename</DropdownMenuItem>
                                        <DropdownMenuItem>Move to</DropdownMenuItem>
                                        <DropdownMenuItem className="text-destructive">
                                            Delete
                                        </DropdownMenuItem>
                                    </DropdownMenuContent>
                                </DropdownMenu>
                            </div>
                        </div>
                    </div>
                ))}
                {downloadingFiles.map((file) => (
                    <DownloadToast
                        key={file.id}
                        filename={file.name}
                        fileSizeMB={file.sizeMB}
                        progress={file.progress}
                        onClose={() => handleCloseToast(file.id)}
                    />
                ))}
            </div>
        </div>
    );
}