"use client";

import { useState, useEffect } from "react";
import { ArrowDown, ArrowUp, Clock, Database, Download } from "lucide-react";
import { Progress } from "./ui/progress";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "./ui/dialog";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "./ui/tabs";
import { backendService } from "../services/BackendService";
import type { DownloadItem } from "../lib/types";
import type { DataUsage, DownloadProgress } from "../services/BackendService";
import {
  formatFileSize,
  formatSpeed,
  formatDuration,
  calculatePercentage,
} from "../lib/utils";

interface DownloadManagerProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  activeDownloadIds: string[];
}

export function DownloadManager({
  open,
  onOpenChange,
  activeDownloadIds,
}: DownloadManagerProps) {
  const [sortField, setSortField] = useState<
    "speed" | "downloadedBytes" | "time"
  >("time");
  const [sortDirection, setSortDirection] = useState<"asc" | "desc">("desc");
  const [dataUsage, setDataUsage] = useState<DataUsage | null>(null);
  const [activeTab, setActiveTab] = useState("downloads");
  const [downloads, setDownloads] = useState<DownloadItem[]>([]);

  // Fetch data usage and download progress
  useEffect(() => {
    if (!open) return;

    const fetchDataUsage = async () => {
      try {
        const data = await backendService.GetDataUsage();
        setDataUsage(data);
      } catch (error) {
        console.error("Failed to fetch data usage:", error);
      }
    };

    const fetchDownloadProgress = async () => {
      try {
        // Get all active downloads from the mock service
        // In a real implementation, you would need to track this info in your app
        const mockService = backendService as any;
        const allDownloads = mockService.getAllActiveDownloads
          ? mockService.getAllActiveDownloads()
          : new Map();

        // Process individual file downloads
        const downloadItems: DownloadItem[] = [];

        // Only process downloads that are in the activeDownloadIds array
        for (const downloadId of activeDownloadIds) {
          const downloadInfo = allDownloads.get(downloadId);
          if (!downloadInfo) continue;

          const progress = downloadInfo.progress as DownloadProgress;
          const containerGuid = downloadInfo.containerGuid;
          const now = new Date();
          const startTime = downloadInfo.startTime || now;
          const elapsedTime = now.getTime() - startTime.getTime();
          const speed = downloadInfo.speed || 0;

          // Add to file downloads
          downloadItems.push({
            id: downloadId,
            fileId: progress.fileId,
            containerGuid,
            fileName: progress.fileName,
            startTime,
            downloadedBytes: progress.receivedBytes,
            totalBytes: progress.totalBytes,
            status: progress.status,
            speed,
            elapsedTime,
          });
        }

        setDownloads(downloadItems);
      } catch (error) {
        console.error("Failed to fetch download progress:", error);
      }
    };

    fetchDataUsage();
    fetchDownloadProgress();

    const interval = setInterval(() => {
      fetchDataUsage();
      fetchDownloadProgress();
    }, 1000);

    return () => clearInterval(interval);
  }, [open, activeDownloadIds]);

  // Sort downloads based on current sort settings
  const sortedDownloads = [...downloads].sort((a, b) => {
    let comparison = 0;

    switch (sortField) {
      case "speed":
        comparison = a.speed - b.speed;
        break;
      case "downloadedBytes":
        comparison = a.downloadedBytes - b.downloadedBytes;
        break;
      case "time":
        comparison = a.startTime.getTime() - b.startTime.getTime();
        break;
    }

    return sortDirection === "asc" ? comparison : -comparison;
  });

  // Toggle sort direction
  const toggleSort = (field: "speed" | "downloadedBytes" | "time") => {
    if (sortField === field) {
      setSortDirection(sortDirection === "asc" ? "desc" : "asc");
    } else {
      setSortField(field);
      setSortDirection("desc"); // Default to descending when changing fields
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[600px] max-h-[80vh] overflow-hidden flex flex-col">
        <DialogHeader>
          <DialogTitle>Transfer Manager</DialogTitle>
        </DialogHeader>

        <Tabs
          value={activeTab}
          onValueChange={setActiveTab}
          className="flex-1 overflow-hidden flex flex-col"
        >
          <TabsList className="grid grid-cols-2">
            <TabsTrigger value="downloads" className="flex items-center gap-1">
              <Download className="h-4 w-4" />
              Downloads
            </TabsTrigger>
            <TabsTrigger value="stats" className="flex items-center gap-1">
              <Database className="h-4 w-4" />
              Data Usage
            </TabsTrigger>
          </TabsList>

          <TabsContent value="downloads" className="flex-1 overflow-auto">
            {sortedDownloads.length === 0 ? (
              <div className="flex flex-col items-center justify-center h-64 text-muted-foreground">
                <Download className="h-12 w-12 mb-4 opacity-20" />
                <p>No active downloads</p>
              </div>
            ) : (
              <div className="space-y-4">
                <div className="flex justify-between text-sm font-medium px-2 py-1 border-b">
                  <div className="w-1/3">File</div>
                  <div
                    className="w-1/5 cursor-pointer flex items-center"
                    onClick={() => toggleSort("downloadedBytes")}
                  >
                    Progress
                    {sortField === "downloadedBytes" &&
                      (sortDirection === "asc" ? (
                        <ArrowUp className="h-3 w-3 ml-1" />
                      ) : (
                        <ArrowDown className="h-3 w-3 ml-1" />
                      ))}
                  </div>
                  <div
                    className="w-1/5 cursor-pointer flex items-center"
                    onClick={() => toggleSort("speed")}
                  >
                    Speed
                    {sortField === "speed" &&
                      (sortDirection === "asc" ? (
                        <ArrowUp className="h-3 w-3 ml-1" />
                      ) : (
                        <ArrowDown className="h-3 w-3 ml-1" />
                      ))}
                  </div>
                  <div
                    className="w-1/5 cursor-pointer flex items-center"
                    onClick={() => toggleSort("time")}
                  >
                    Time
                    {sortField === "time" &&
                      (sortDirection === "asc" ? (
                        <ArrowUp className="h-3 w-3 ml-1" />
                      ) : (
                        <ArrowDown className="h-3 w-3 ml-1" />
                      ))}
                  </div>
                </div>

                {sortedDownloads.map((download) => {
                  const percentage = calculatePercentage(
                    download.downloadedBytes,
                    download.totalBytes,
                  );

                  return (
                    <div
                      key={download.id}
                      className="border rounded-md p-3 space-y-2"
                    >
                      <div className="flex justify-between items-center">
                        <div className="font-medium truncate w-2/3">
                          {download.fileName}
                        </div>
                        <div className="text-xs text-muted-foreground">
                          {download.status === "active" ? (
                            <span className="text-green-600 font-medium">
                              Downloading
                            </span>
                          ) : download.status === "completed" ? (
                            <span className="text-blue-600 font-medium">
                              Completed
                            </span>
                          ) : (
                            <span className="text-red-600 font-medium">
                              Failed
                            </span>
                          )}
                        </div>
                      </div>

                      <div className="space-y-1">
                        <div className="flex justify-between text-xs text-muted-foreground">
                          <span>
                            {formatFileSize(download.downloadedBytes)} of{" "}
                            {formatFileSize(download.totalBytes)}
                          </span>
                          <span>{percentage}%</span>
                        </div>
                        <Progress value={percentage} className="h-2" />
                      </div>

                      <div className="flex justify-between text-sm">
                        <div className="flex items-center gap-1">
                          <Download className="h-3 w-3 text-muted-foreground" />
                          <span>{formatSpeed(download.speed)}</span>
                        </div>
                        <div className="flex items-center gap-1">
                          <Clock className="h-3 w-3 text-muted-foreground" />
                          <span>{formatDuration(download.elapsedTime)}</span>
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </TabsContent>

          <TabsContent value="stats" className="flex-1 overflow-auto">
            {!dataUsage ? (
              <div className="flex items-center justify-center h-64">
                <div className="w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
              </div>
            ) : (
              <div className="space-y-6">
                <div className="border rounded-md p-4">
                  <h3 className="text-lg font-medium mb-4">
                    Data Usage Summary
                  </h3>
                  <div className="grid grid-cols-2 gap-6">
                    <div className="space-y-2 border rounded-md p-4">
                      <div className="flex items-center text-blue-500">
                        <Download className="h-5 w-5 mr-2" />
                        <h4 className="font-medium">Downloaded</h4>
                      </div>
                      <div className="text-2xl font-semibold">
                        {formatFileSize(dataUsage.totalBytesReceived)}
                      </div>
                    </div>

                    <div className="space-y-2 border rounded-md p-4">
                      <div className="flex items-center text-green-500">
                        <ArrowUp className="h-5 w-5 mr-2" />
                        <h4 className="font-medium">Uploaded</h4>
                      </div>
                      <div className="text-2xl font-semibold">
                        {formatFileSize(dataUsage.totalBytesSent)}
                      </div>
                    </div>

                    <div className="col-span-2 space-y-2 border rounded-md p-4">
                      <h4 className="font-medium">Total Data Transferred</h4>
                      <div className="text-2xl font-semibold">
                        {formatFileSize(
                          dataUsage.totalBytesReceived +
                            dataUsage.totalBytesSent,
                        )}
                      </div>

                      <div className="mt-4">
                        <div className="flex justify-between text-sm mb-1">
                          <span>Download</span>
                          <span>Upload</span>
                        </div>
                        <div className="h-2 w-full bg-muted rounded-full overflow-hidden">
                          <div
                            className="h-full bg-blue-500 rounded-full"
                            style={{
                              width: `${calculatePercentage(
                                dataUsage.totalBytesReceived,
                                dataUsage.totalBytesReceived +
                                  dataUsage.totalBytesSent,
                              )}%`,
                            }}
                          />
                        </div>
                        <div className="flex justify-between text-xs text-muted-foreground mt-1">
                          <span>
                            {formatFileSize(dataUsage.totalBytesReceived)}
                          </span>
                          <span>
                            {formatFileSize(dataUsage.totalBytesSent)}
                          </span>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            )}
          </TabsContent>
        </Tabs>
      </DialogContent>
    </Dialog>
  );
}
