"use client";

import { useState, useEffect } from "react";
import { DownloadToast } from "./ui/download-toast";
import { ChevronLeft, MoreHorizontal } from "lucide-react";
import { Button } from "./ui/button";
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuTrigger,
} from "./ui/dropdown-menu";
import type { File, Folder } from "../lib/types";

interface FileGridProps {
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

export function FileGrid({
    files,
    folders,
    currentFolder,
    currentFolderName,
    navigateToFolder,
    navigateToParent,
}: FileGridProps) {
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

    // Simuliuojam siuntimo progresą
    useEffect(() => {
        const interval = setInterval(() => {
            setDownloadingFiles((prev) =>
                prev.map((file) => {
                    const fakeSpeed = 0.2; // 0.2 MB/s
                    const added = (fakeSpeed / file.sizeMB) * 100;
                    const nextProgress = file.progress + added;
                    return {
                        ...file,
                        progress: nextProgress >= 100 ? 100 : nextProgress,
                    };
                }).filter(f => f.progress < 100)
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

            <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
                {folders.map((folder) => (
                    <div key={folder.id} className="group relative">
                        <div
                            className="flex flex-col items-center p-4 rounded-lg border bg-background hover:bg-muted/50 cursor-pointer"
                            onClick={() => navigateToFolder(folder.id)}
                        >
                            <div className="h-16 w-16 flex items-center justify-center text-blue-500">
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
                                    className="h-12 w-12"
                                >
                                    <path d="M4 20h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.93a2 2 0 0 1-1.66-.9l-.82-1.2A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13c0 1.1.9 2 2 2Z" />
                                </svg>
                            </div>
                            <div className="mt-2 w-full text-center truncate">{folder.name}</div>
                        </div>
                        <div className="absolute top-2 right-2 opacity-0 group-hover:opacity-100">
                            <DropdownMenu>
                                <DropdownMenuTrigger asChild>
                                    <Button variant="ghost" size="icon" className="h-8 w-8">
                                        <MoreHorizontal className="h-4 w-4" />
                                        <span className="sr-only">More</span>
                                    </Button>
                                </DropdownMenuTrigger>
                                <DropdownMenuContent align="end">
                                    <DropdownMenuItem>Rename</DropdownMenuItem>
                                    <DropdownMenuItem>Move to</DropdownMenuItem>
                                    <DropdownMenuItem className="text-destructive">Delete</DropdownMenuItem>
                                </DropdownMenuContent>
                            </DropdownMenu>
                        </div>
                    </div>
                ))}

                {files.map((file) => (
                    <div key={file.id} className="group relative">
                        <div className="flex flex-col items-center p-4 rounded-lg border bg-background hover:bg-muted/50 cursor-pointer">
                            <div className="h-16 w-16 flex items-center justify-center">
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
                                    className="h-12 w-12 text-gray-500"
                                >
                                    <path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5L14.5 2z" />
                                    <polyline points="14 2 14 8 20 8" />
                                </svg>
                            </div>
                            <div className="mt-2 w-full text-center truncate">{file.name}</div>
                        </div>
                        <div className="absolute top-2 right-2 opacity-0 group-hover:opacity-100">
                            <DropdownMenu>
                                <DropdownMenuTrigger asChild>
                                    <Button variant="ghost" size="icon" className="h-8 w-8">
                                        <MoreHorizontal className="h-4 w-4" />
                                        <span className="sr-only">More</span>
                                    </Button>
                                </DropdownMenuTrigger>
                                <DropdownMenuContent align="end">
                                    <DropdownMenuItem>Open</DropdownMenuItem>
                                    <DropdownMenuItem onClick={() => handleDownload(file)}>Download</DropdownMenuItem>
                                    <DropdownMenuItem>Rename</DropdownMenuItem>
                                    <DropdownMenuItem>Move to</DropdownMenuItem>
                                    <DropdownMenuItem className="text-destructive">Delete</DropdownMenuItem>
                                </DropdownMenuContent>
                            </DropdownMenu>
                        </div>
                    </div>
                ))}
            </div>

            {/* Atsisiuntimo langeliai */}
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
    );
}