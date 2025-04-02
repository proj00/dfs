"use client";

import { X, ChevronDown, ChevronUp } from "lucide-react";
import { useState } from "react";

interface DownloadToastProps {
    filename: string;
    fileSizeMB: number;
    progress: number;
    onClose: () => void;
}

export function DownloadToast({
    filename,
    fileSizeMB,
    progress,
    onClose,
}: DownloadToastProps) {
    const [expanded, setExpanded] = useState(false);

    return (
        <div className="fixed bottom-4 right-4 bg-white border rounded shadow-md p-4 w-80 z-50 animate-slide-in">
            <div className="flex justify-between items-center mb-2">
                <span className="font-semibold text-sm text-gray-800">
                    {progress >= 100 ? "Download complete" : "Downloading"}
                </span>
                <button onClick={onClose}>
                    <X className="h-4 w-4 text-gray-500 hover:text-gray-700" />
                </button>
            </div>

            <div className="flex justify-between items-center">
                <div className="text-sm text-gray-700 truncate">{filename}</div>
                <button onClick={() => setExpanded(!expanded)}>
                    {expanded ? (
                        <ChevronUp className="h-4 w-4 text-gray-500" />
                    ) : (
                        <ChevronDown className="h-4 w-4 text-gray-500" />
                    )}
                </button>
            </div>

            {expanded && (
                <div className="mt-3 text-sm text-gray-600 space-y-2">
                    <div className="w-full bg-gray-200 rounded h-2">
                        <div
                            className="bg-blue-500 h-2 rounded"
                            style={{ width: `${progress}%` }}
                        ></div>
                    </div>
                    <div>Progress: {Math.floor(progress)}%</div>
                    <div>Status: {progress >= 100 ? "Finished" : "Downloading..."}</div>
                    <div>Speed: 0.2 MB/s</div> // mock speed
                </div>
            )}
        </div>
    );
}