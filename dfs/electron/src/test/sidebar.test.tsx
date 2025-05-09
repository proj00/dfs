import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { Sidebar } from "../components/sidebar"; // adjust if needed
import * as BackendModule from "../IpcService/BackendService";


jest.mock("electron-log/renderer", () => ({
  error: jest.fn(),
  info: jest.fn(),
}));

jest.mock("../lib/utils", () => ({
  ...jest.requireActual("../lib/utils"),
  calculatePercentage: () => 50,
  formatFileSize: (bytes: number) => `${bytes} B`,
  formatProgress: (received: number, total: number) =>
    `${received} / ${total} B`,
  createPollCallback: (fn: any) => fn,
}));

jest.mock("../components/download-manager", () => ({
  DownloadManager: () => <div data-testid="download-manager" />,
}));

jest.mock("../components/blocked-peers-dialog", () => ({
  BlockedPeersDialog: ({ open }: { open: boolean }) =>
    open ? <div data-testid="blocked-peers-dialog" /> : null,
}));

const mockDataUsage = {
  totalBytesSent: 1024,
  totalBytesReceived: 2048,
};

const mockFolders = [
  {
    id: "root1",
    name: "Root Folder 1",
    parentId: [""],
    hasChildren: false,
    containerGuid: "guid1",
    createdAt: "",
    modifiedAt: "",
  },
  {
    id: "child1",
    name: "Child Folder",
    parentId: ["root1"],
    hasChildren: false,
    containerGuid: "guid2",
    createdAt: "",
    modifiedAt: "",
  },
];

describe("Sidebar", () => {
  beforeEach(() => {
    jest
      .spyOn(BackendModule.backendService, "GetDataUsage")
      .mockResolvedValue(mockDataUsage);
    jest
      .spyOn(BackendModule.backendService, "downloadContainer")
      .mockResolvedValue(["file1"]);
    jest
      .spyOn(BackendModule.backendService, "publishToTracker")
      .mockResolvedValue(true);
    jest
      .spyOn(BackendModule.backendService, "GetDownloadProgress")
      .mockResolvedValue({
        fileId: "file1",
        receivedBytes: 1024,
        totalBytes: 1024,
        status: "completed",
        fileName: "file.txt",
      });
  });

  it("renders folders and data usage", async () => {
    render(
      <Sidebar
        currentFolder={null}
        navigateToFolder={jest.fn()}
        folders={mockFolders}
      />,
    );

    expect(screen.getByText("My Drive")).toBeInTheDocument();
    expect(screen.getByText("Root Folder 1")).toBeInTheDocument();
    expect(screen.queryByText("Child Folder")).not.toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText("2048 B")).toBeInTheDocument(); // Downloaded
      expect(screen.getByText("1024 B")).toBeInTheDocument(); // Uploaded
      expect(screen.getByText("3072 B")).toBeInTheDocument(); // Total
    });
  });

  it("opens and closes publish dialog", async () => {
    render(
      <Sidebar
        currentFolder={null}
        navigateToFolder={jest.fn()}
        folders={mockFolders}
      />,
    );

    fireEvent.click(screen.getByText("Publish to Tracker"));
    expect(screen.getByText("Publish to Tracker")).toBeInTheDocument();

    fireEvent.change(screen.getByDisplayValue(""), {
      target: { value: "guid1" },
    });
    fireEvent.change(screen.getByPlaceholderText("Enter tracker URI"), {
      target: { value: "tracker://uri" },
    });

    fireEvent.click(screen.getByText("Publish"));
    await waitFor(() => {
      expect(screen.getByText("Container published successfully!")).toBeInTheDocument();
    });
  });

  it("opens and closes download dialog", async () => {
    render(
      <Sidebar
        currentFolder={null}
        navigateToFolder={jest.fn()}
        folders={mockFolders}
      />,
    );

    fireEvent.click(screen.getByText("Download Container"));

    fireEvent.change(screen.getByPlaceholderText("Enter container GUID"), {
      target: { value: "guid1" },
    });
    fireEvent.change(screen.getByPlaceholderText("Enter tracker URI"), {
      target: { value: "tracker://uri" },
    });

    fireEvent.click(screen.getByText("Download"));
    await waitFor(() => {
      expect(screen.getByText("Download started successfully!")).toBeInTheDocument();
    });
  });

  it("opens transfer manager and blocked peers", async () => {
    render(
      <Sidebar
        currentFolder={null}
        navigateToFolder={jest.fn()}
        folders={mockFolders}
      />,
    );

    fireEvent.click(screen.getByText("Transfer Manager"));
    expect(screen.getByTestId("download-manager")).toBeInTheDocument();

    fireEvent.click(screen.getByText("Blocked Peers"));
    expect(screen.getByTestId("blocked-peers-dialog")).toBeInTheDocument();
  });

  it("calls navigateToFolder when clicking folder", () => {
    const navigateToFolder = jest.fn();
    render(
      <Sidebar
        currentFolder={null}
        navigateToFolder={navigateToFolder}
        folders={mockFolders}
      />,
    );

    fireEvent.click(screen.getByText("Root Folder 1"));
    expect(navigateToFolder).toHaveBeenCalledWith("root1");
  });
});
