test.todo('write some tests');
//import React from "react";

import { render, screen, waitFor } from "@testing-library/react";
import { DownloadManager } from "../components/download-manager"; // adjust path as needed
import * as BackendModule from "@/IpcService/BackendService"; // Mock backendService

/**
 * @jest-environment jsdom
 */
// Mock external icons and UI components

jest.mock("lucide-react", () => ({
  Download: () => <div data-testid="icon-download" />,
  ArrowUp: () => <div data-testid="icon-up" />,
  ArrowDown: () => <div data-testid="icon-down" />,
  Clock: () => <div data-testid="icon-clock" />,
  Database: () => <div data-testid="icon-db" />,
}));

jest.mock("electron-log/renderer", () => ({
  error: jest.fn(),
  info: jest.fn(),
}));

jest.mock("../lib/utils", () => ({
  ...jest.requireActual("../lib/utils"),
  calculatePercentage: jest.fn(() => 50),
  formatFileSize: jest.fn((bytes: number) => `${bytes} B`),
  formatSpeed: jest.fn(() => "1 MB/s"),
  formatDuration: jest.fn(() => "1m 30s"),
  createPollCallback: (fn: any, _interval: number) => fn,
}));

const mockDataUsage = {
  totalBytesReceived: 1024,
  totalBytesSent: 2048,
};

const mockDownloads = new Map([
  [
    "download-id-1",
    {
      progress: {
        fileId: "file-1",
        fileName: "example.txt",
        receivedBytes: 512,
        totalBytes: 1024,
        status: "active",
      },
      containerGuid: "container-1",
      startTime: new Date(Date.now() - 60000),
      speed: 1024 * 1024,
    },
  ],
]);

describe("DownloadManager", () => {
  beforeEach(() => {
    jest
      .spyOn(BackendModule.backendService, "GetDataUsage")
      .mockResolvedValue(mockDataUsage);

    (BackendModule.backendService as any).getAllActiveDownloads = () => mockDownloads;
  });

  it("renders correctly with no active downloads", async () => {
    // Provide empty download map
    (BackendModule.backendService as any).getAllActiveDownloads = () => new Map();

    render(
      <DownloadManager
        open={true}
        onOpenChange={() => {}}
        activeDownloadIds={["download-id-1"]}
      />,
    );

    await waitFor(() =>
      expect(screen.getByText("No active downloads")).toBeInTheDocument(),
    );
  });

  it("renders active downloads correctly", async () => {
    render(
      <DownloadManager
        open={true}
        onOpenChange={() => {}}
        activeDownloadIds={["download-id-1"]}
      />,
    );

    await waitFor(() => {
      expect(screen.getByText("example.txt")).toBeInTheDocument();
      expect(screen.getByText("Downloading")).toBeInTheDocument();
      expect(screen.getByText("512 B of 1024 B")).toBeInTheDocument();
      expect(screen.getByText("50%")).toBeInTheDocument();
      expect(screen.getByText("1 MB/s")).toBeInTheDocument();
      expect(screen.getByText("1m 30s")).toBeInTheDocument();
    });
  });

  it("renders data usage tab correctly", async () => {
    render(
      <DownloadManager
        open={true}
        onOpenChange={() => {}}
        activeDownloadIds={["download-id-1"]}
      />,
    );

    await waitFor(() =>
      expect(screen.getByText("Data Usage")).toBeInTheDocument(),
    );

    screen.getByText("Data Usage").click();

    await waitFor(() => {
      expect(screen.getByText("Data Usage Summary")).toBeInTheDocument();
      expect(screen.getAllByText("1024 B").length).toBeGreaterThan(0);
      expect(screen.getByText("2048 B")).toBeInTheDocument();
    });
  });
});


