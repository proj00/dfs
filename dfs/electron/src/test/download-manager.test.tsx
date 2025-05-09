import "@testing-library/jest-dom";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { DownloadManager } from "../components/download-manager";
import { NodeServiceClient } from "@/IpcService/GetNodeService";
import { backendService } from "../IpcService/BackendService";
import { getMockClient } from "./getMockNodeServiceClient";
import { jest } from "@jest/globals";

// Mock the backendService
jest.mock("@/IpcService/GetNodeService", () => ({
  backendService: {
    GetDataUsage: jest.fn().mockResolvedValue({

    }),
    GetDownloadProgress: jest.fn().mockImplementation((downloadId) => {
      // Return different progress for different download IDs
      if (downloadId === "download-1") {
        return Promise.resolve({
          fileId: "file-1",
          receivedBytes: 524288, // 512KB
          totalBytes: 1048576, // 1MB
          status: "active",
          fileName: "document.pdf",
        });
      } else if (downloadId === "download-2") {
        return Promise.resolve({
          fileId: "file-2",
          receivedBytes: 1048576, // 1MB
          totalBytes: 1048576, // 1MB
          status: "completed",
          fileName: "image.jpg",
        });
      } else {
        return Promise.resolve({
          fileId: downloadId,
          receivedBytes: 0,
          totalBytes: 0,
          status: "failed",
          fileName: "Unknown file",
        });
      }
    }),
    getAllActiveDownloads: jest.fn().mockReturnValue(
      new Map([
        [
          "download-1",
          {
            progress: {
              fileId: "file-1",
              receivedBytes: 524288,
              totalBytes: 1048576,
              status: "active",
              fileName: "document.pdf",
            },
            startTime: new Date(Date.now() - 60000), // Started 1 minute ago
            lastUpdate: Date.now(),
            speed: 8192, // 8KB/s
            containerGuid: "container-1",
          },
        ],
        [
          "download-2",
          {
            progress: {
              fileId: "file-2",
              receivedBytes: 1048576,
              totalBytes: 1048576,
              status: "completed",
              fileName: "image.jpg",
            },
            startTime: new Date(Date.now() - 120000), // Started 2 minutes ago
            lastUpdate: Date.now(),
            speed: 0, // Completed
            containerGuid: "container-1",
          },
        ],
      ])
    ),
  },
}));

describe("DownloadManager", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it("renders the download manager when open is true", () => {
    render(
      <DownloadManager
        open={true}
        onOpenChange={() => {}}
        activeDownloadIds={[]}
      />
    );
    expect(screen.getByText("Transfer Manager")).toBeInTheDocument();
  });

  it("does not render the download manager when open is false", () => {
    render(
      <DownloadManager
        open={false}
        onOpenChange={() => {}}
        activeDownloadIds={[]}
      />
    );
    expect(screen.queryByText("Transfer Manager")).not.toBeInTheDocument();
  });

  it("displays active downloads", async () => {
    render(
      <DownloadManager
        open={true}
        onOpenChange={() => {}}
        activeDownloadIds={["download-1", "download-2"]}
      />
    );

    // Wait for downloads to be displayed
    await waitFor(() => {
      expect(screen.getByText("document.pdf")).toBeInTheDocument();
      expect(screen.getByText("image.jpg")).toBeInTheDocument();
    });

    // Verify download status is displayed
    expect(screen.getByText("Downloading")).toBeInTheDocument();
    expect(screen.getByText("Completed")).toBeInTheDocument();

    // Verify progress information is displayed
    expect(screen.getByText("50%")).toBeInTheDocument();
    expect(screen.getByText("100%")).toBeInTheDocument();
  });

  it("displays data usage statistics", async () => {
    render(
      <DownloadManager
        open={true}
        onOpenChange={() => {}}
        activeDownloadIds={[]}
      />
    );

    // Switch to stats tab
    fireEvent.click(screen.getByRole("tab", { name: "Data Usage" }));

    // Wait for data usage to be displayed
    await waitFor(() => {
      expect(screen.getByText("Data Usage Summary")).toBeInTheDocument();
      expect(screen.getByText("5 MB")).toBeInTheDocument(); // Downloaded
      expect(screen.getByText("1 MB")).toBeInTheDocument(); // Uploaded
      expect(screen.getByText("6 MB")).toBeInTheDocument(); // Total
    });
  });
  /*
  it("sorts downloads based on selected criteria", async () => {
    render(<DownloadManager open={true} onOpenChange={() => {}} activeDownloadIds={["download-1", "download-2"]} />)

    // Wait for downloads to be displayed
    await waitFor(() => {
      expect(screen.getByText("document.pdf")).toBeInTheDocument()
      expect(screen.getByText("image.jpg")).toBeInTheDocument()
    })

    // Sort by speed
    fireEvent.click(screen.getByText("Speed"))

    // Sort by progress
    fireEvent.click(screen.getByText("Progress"))

    // Sort by time
    fireEvent.click(screen.getByText("Time"))

    // Verify backendService methods were called
    expect(GetNodeService.GetDataUsage).toHaveBeenCalled()
    expect(GetNodeService.getAllActiveDownloads).toHaveBeenCalled()
  })*/

  it("shows empty state when no downloads are active", async () => {
    render(
      <DownloadManager
        open={true}
        onOpenChange={() => {}}
        activeDownloadIds={[]}
      />
    );

    // Wait for empty state to be displayed
    await waitFor(() => {
      expect(screen.getByText("No active downloads")).toBeInTheDocument();
    });
  });
});
