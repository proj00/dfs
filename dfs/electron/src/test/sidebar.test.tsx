import "@testing-library/jest-dom";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { Sidebar } from "../components/sidebar";
import { GetNodeService } from "@/IpcService/GetNodeService";
import { jest } from "@jest/globals";

// Mock the GetNodeService function
jest.mock("@/IpcService/GetNodeService", () => ({
  GetNodeService: jest.fn().mockResolvedValue({
    GetDataUsage: jest.fn().mockResolvedValue({
      totalBytesSent: "1048576", // 1MB
      totalBytesReceived: "5242880", // 5MB
    }),
    PublishToTracker: jest.fn().mockResolvedValue(undefined),
    DownloadContainer: jest
      .fn()
      .mockResolvedValue(["download-id-1", "download-id-2"]),
    RevealLogFile: jest.fn().mockResolvedValue(undefined),
  }),
}));

// Mock the backendService
jest.mock("../services/BackendService", () => ({
  backendService: {
    GetDataUsage: jest.fn().mockResolvedValue({
      totalBytesSent: 1048576, // 1MB
      totalBytesReceived: 5242880, // 5MB
    }),
  },
}));

// Mock the DownloadManager component
jest.mock("../components/download-manager", () => ({
  DownloadManager: jest.fn(() => (
    <div data-testid="download-manager">Download Manager</div>
  )),
}));

// Mock the BlockedPeersDialog component
jest.mock("../components/blocked-peers-dialog", () => ({
  BlockedPeersDialog: jest.fn(() => (
    <div data-testid="blocked-peers-dialog">Blocked Peers Dialog</div>
  )),
}));

describe("Sidebar", () => {
  const mockFolders = [
    {
      id: "folder-1",
      name: "Work Documents",
      parentId: [], // Changed from null to empty array
      createdAt: "2023-01-15T10:30:00Z",
      modifiedAt: "2023-03-20T14:15:00Z",
      hasChildren: true,
      containerGuid: "container-guid-1",
    },
    {
      id: "folder-2",
      name: "Personal",
      parentId: [], // Changed from null to empty array
      createdAt: "2023-02-05T09:45:00Z",
      modifiedAt: "2023-04-10T11:20:00Z",
      hasChildren: true,
      containerGuid: "container-guid-2",
    },
    {
      id: "folder-3",
      name: "Projects",
      parentId: ["folder-1"], // Changed from string to array with string
      createdAt: "2023-02-10T13:20:00Z",
      modifiedAt: "2023-04-15T16:30:00Z",
      hasChildren: false,
      containerGuid: "container-guid-3",
    },
  ];

  const defaultProps = {
    currentFolder: null,
    navigateToFolder: jest.fn(),
    folders: mockFolders,
    onContainerDownloaded: jest.fn(),
    onContainerPublished: jest.fn(),
  };

  beforeEach(() => {
    jest.clearAllMocks();
    // Setup fake timers for setTimeout tests
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it("renders the sidebar with folders", () => {
    render(<Sidebar {...defaultProps} />);

    // Check if main sections are rendered
    expect(screen.getByText("My Drive")).toBeInTheDocument();
    expect(screen.getByText("Recent")).toBeInTheDocument();
    expect(screen.getByText("Starred")).toBeInTheDocument();

    // Check if action buttons are rendered
    expect(screen.getByText("Publish to Tracker")).toBeInTheDocument();
    expect(screen.getByText("Download Container")).toBeInTheDocument();
    expect(screen.getByText("Transfer Manager")).toBeInTheDocument();
    expect(screen.getByText("Open Logs")).toBeInTheDocument();
    expect(screen.getByText("Blocked Peers")).toBeInTheDocument();

    // Check if folders are rendered
    expect(screen.getByText("My folders")).toBeInTheDocument();
    expect(screen.getByText("Work Documents")).toBeInTheDocument();
    expect(screen.getByText("Personal")).toBeInTheDocument();
  });

  it("navigates to a folder when clicked", () => {
    const navigateToFolder = jest.fn();

    render(<Sidebar {...defaultProps} navigateToFolder={navigateToFolder} />);

    // Click on a folder
    fireEvent.click(screen.getByText("Work Documents"));

    // Verify navigateToFolder was called with correct folder ID
    expect(navigateToFolder).toHaveBeenCalledWith("folder-1");
  });

  it("opens the publish dialog when Publish to Tracker is clicked", () => {
    render(<Sidebar {...defaultProps} />);

    // Click on Publish to Tracker
    fireEvent.click(screen.getByText("Publish to Tracker"));

    // Verify dialog is opened
    expect(
      screen.getByText(
        "Select a container and enter a tracker URI to publish your container."
      )
    ).toBeInTheDocument();
  });

  it("opens the download dialog when Download Container is clicked", () => {
    render(<Sidebar {...defaultProps} />);

    // Click on Download Container
    fireEvent.click(screen.getByText("Download Container"));

    // Verify dialog is opened
    expect(
      screen.getByText(
        "Enter a container GUID and tracker URI to download a container."
      )
    ).toBeInTheDocument();
  });

  it("publishes a container when form is submitted", async () => {
    const onContainerPublished = jest.fn();

    render(
      <Sidebar {...defaultProps} onContainerPublished={onContainerPublished} />
    );

    // Open publish dialog
    fireEvent.click(screen.getByText("Publish to Tracker"));

    // Fill in the form
    fireEvent.change(screen.getByRole("combobox"), {
      target: { value: "folder-1" },
    });
    fireEvent.change(screen.getByPlaceholderText("Enter tracker URI"), {
      target: { value: "tracker.example.com" },
    });

    // Submit the form
    fireEvent.click(screen.getByText("Publish"));

    // Verify PublishToTracker was called with correct parameters
    const nodeService = await GetNodeService();
    expect(nodeService.PublishToTracker).toHaveBeenCalledWith({
      containerGuid: "folder-1",
      trackerUri: "tracker.example.com",
    });

    // Wait for success message
    await waitFor(() => {
      expect(
        screen.getByText("Container published successfully!")
      ).toBeInTheDocument();
    });

    // Wait for dialog to close and callback to be called
    jest.advanceTimersByTime(2000);
    expect(onContainerPublished).toHaveBeenCalled();
  });

  it("downloads a container when form is submitted", async () => {
    const onContainerDownloaded = jest.fn();

    render(
      <Sidebar
        {...defaultProps}
        onContainerDownloaded={onContainerDownloaded}
      />
    );

    // Open download dialog
    fireEvent.click(screen.getByText("Download Container"));

    // Fill in the form
    fireEvent.change(screen.getByPlaceholderText("Enter container GUID"), {
      target: { value: "container-guid-123" },
    });
    fireEvent.change(screen.getByPlaceholderText("Enter tracker URI"), {
      target: { value: "tracker.example.com" },
    });

    // Submit the form
    fireEvent.click(screen.getByRole("button", { name: "Download" }));

    // Verify DownloadContainer was called with correct parameters
    const nodeService = await GetNodeService();
    expect(nodeService.DownloadContainer).toHaveBeenCalledWith({
      containerGuid: "container-guid-123",
      trackerUri: "tracker.example.com",
      destination: undefined,
    });

    // Wait for success message
    await waitFor(() => {
      expect(
        screen.getByText("Download started successfully!")
      ).toBeInTheDocument();
    });
  });

  it("opens logs when Open Logs is clicked", async () => {
    render(<Sidebar {...defaultProps} />);

    // Click on Open Logs
    fireEvent.click(screen.getByText("Open Logs"));

    // Verify RevealLogFile was called
    const nodeService = await GetNodeService();
    expect(nodeService.RevealLogFile).toHaveBeenCalled();
  });

  it("opens blocked peers dialog when Blocked Peers is clicked", () => {
    render(<Sidebar {...defaultProps} />);

    // Click on Blocked Peers
    fireEvent.click(screen.getByText("Blocked Peers"));

    // Verify dialog is opened
    expect(screen.getByTestId("blocked-peers-dialog")).toBeInTheDocument();
  });

  it("displays data usage when available", async () => {
    render(<Sidebar {...defaultProps} />);

    // Wait for data usage to be displayed
    await waitFor(() => {
      expect(screen.getByText("Data Usage")).toBeInTheDocument();
      expect(screen.getByText("5 MB")).toBeInTheDocument(); // Downloaded
      expect(screen.getByText("1 MB")).toBeInTheDocument(); // Uploaded
      expect(screen.getByText("6 MB")).toBeInTheDocument(); // Total
    });
  });
});
