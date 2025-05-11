import "@testing-library/jest-dom";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { TrackerSearchDialog } from "../components/tracker-search-dialog";
import { GetNodeService } from "@/IpcService/GetNodeService";
import { jest } from "@jest/globals";
import { getMockClient } from "./getMockNodeServiceClient";
// Mock the GetNodeService function


 
jest.mock("@/IpcService/GetNodeService", () => {
  let mock = getMockClient();
  
  mock.SearchForObjects.mockResolvedValue({
    results: [
      {
        guid: "guid-123", // Added guid field
        object: {
          object: {
            name: "Test File",
            type: {
              oneofKind: "file",
              file: {
                // Assuming File has some properties, add them here
                // Example properties for the File object
                size: BigInt(1048576), // 1MB
                hashes: undefined,
              },
            },
          },
          hash: new Uint8Array([1, 2, 3, 4]), // Example hash
        },
      },
      {
        guid: "guid-456", // Added guid field
        object: {
          object: {
            name: "Another Directory",
            type: {
              oneofKind: "directory",
              directory: {
                entries: [] //??
              },
            },
          },
          hash: new Uint8Array([5, 6, 7, 8]), // Example hash
        },
      },
    ],
  });
  return mock;
});
/*
jest.mock("@/IpcService/GetNodeService", () => {
  return {
    SearchForObjects: jest.fn().mockRejectedValue(new Error("Search failed") as never), //??
  };
});
*/
// Mock clipboard API
Object.assign(navigator, {
  clipboard: {
    writeText: jest.fn<(s: string) => Promise<void>>().mockResolvedValue(),
  },
});

describe("TrackerSearchDialog", () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it("renders the dialog when open is true", () => {
    render(<TrackerSearchDialog open={true} onOpenChange={() => {}} />);
    expect(screen.getByText("Search Tracker")).toBeInTheDocument();
  });

  it("does not render the dialog when open is false", () => {
    render(<TrackerSearchDialog open={false} onOpenChange={() => {}} />);
    expect(screen.queryByText("Search Tracker")).not.toBeInTheDocument();
  });

  it("performs a search when search button is clicked", async () => {
    render(<TrackerSearchDialog open={true} onOpenChange={() => {}} />);

    // Fill in the form
    fireEvent.change(screen.getByPlaceholderText("Enter tracker URI"), {
      target: { value: "tracker.example.com" },
    });
    fireEvent.change(screen.getByPlaceholderText("Enter search terms"), {
      target: { value: "test" },
    });

    // Click search button
    fireEvent.click(screen.getByText("Search"));

    // Verify SearchForObjects was called with correct parameters
    const nodeService = await GetNodeService();
    expect(nodeService.SearchForObjects).toHaveBeenCalledWith({
      query: "test",
      trackerUri: "tracker.example.com",
    });

    // Verify search results are displayed
    await waitFor(() => {
      expect(screen.getByText("Test Container")).toBeInTheDocument();
      expect(screen.getByText("Another Container")).toBeInTheDocument();
      expect(screen.getByText("A test container")).toBeInTheDocument();
      expect(screen.getByText("Size: 1 MB")).toBeInTheDocument();
      expect(screen.getByText("Size: 5 MB")).toBeInTheDocument();
    });
  });

  it("handles copying container GUID", async () => {
    render(<TrackerSearchDialog open={true} onOpenChange={() => {}} />);

    // Fill in the form and search
    fireEvent.change(screen.getByPlaceholderText("Enter tracker URI"), {
      target: { value: "tracker.example.com" },
    });
    fireEvent.change(screen.getByPlaceholderText("Enter search terms"), {
      target: { value: "test" },
    });
    fireEvent.click(screen.getByText("Search"));

    // Wait for search results
    await waitFor(() => {
      expect(screen.getByText("Test Container")).toBeInTheDocument();
    });

    // Find and click the copy button for the first result
    const copyButtons = screen.getAllByRole("button", { name: "" });
    fireEvent.click(copyButtons[0]);

    // Verify clipboard API was called with correct GUID
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith(
      "container-guid-123"
    );
  });

  it("calls onDownloadContainer when download button is clicked", async () => {
    const onDownloadContainer = jest.fn();

    render(
      <TrackerSearchDialog
        open={true}
        onOpenChange={() => {}}
        onDownloadContainer={onDownloadContainer}
      />
    );

    // Fill in the form and search
    fireEvent.change(screen.getByPlaceholderText("Enter tracker URI"), {
      target: { value: "tracker.example.com" },
    });
    fireEvent.change(screen.getByPlaceholderText("Enter search terms"), {
      target: { value: "test" },
    });
    fireEvent.click(screen.getByText("Search"));

    // Wait for search results
    await waitFor(() => {
      expect(screen.getByText("Test Container")).toBeInTheDocument();
    });

    // Find and click the download button for the first result
    const downloadButtons = screen.getAllByText("Download");
    fireEvent.click(downloadButtons[0]);

    // Verify onDownloadContainer was called with correct parameters
    expect(onDownloadContainer).toHaveBeenCalledWith(
      "container-guid-123",
      "tracker.example.com"
    );
  });


    it("handles search failure", async () => {
    render(<TrackerSearchDialog open={true} onOpenChange={() => {}} />);

    // Fill in the form
    fireEvent.change(screen.getByPlaceholderText("Enter tracker URI"), {
      target: { value: "tracker.example.com" },
    });
    fireEvent.change(screen.getByPlaceholderText("Enter search terms"), {
      target: { value: "test" },
    });

    // Click search button
    fireEvent.click(screen.getByText("Search"));

    // Verify error message is displayed
    await waitFor(() => {
      expect(
        screen.getByText(
          "Failed to search tracker. Please check the tracker URI and try again."
        )
      ).toBeInTheDocument();
    });
  });
});
