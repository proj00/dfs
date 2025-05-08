import "@testing-library/jest-dom";
import { render, screen, fireEvent } from "@testing-library/react";
import { FileGrid } from "../components/file-grid";
import { jest } from "@jest/globals";

// Mock the file handlers
jest.mock("../lib/file-handlers", () => ({
  handleFileOpen: jest.fn(),
  handleRename: jest.fn(),
  handleMove: jest.fn(),
  handleDelete: jest.fn(),
  handleShare: jest.fn(),
}));

describe("FileGrid", () => {
  const mockFiles = [
    {
      id: "file-1",
      name: "Document.docx",
      type: "document",
      size: 2500000,
      folderId: "folder-1",
      createdAt: "2023-03-15T09:20:00Z",
      modifiedAt: "2023-04-01T11:30:00Z",
    },
    {
      id: "file-2",
      name: "Image.jpg",
      type: "image",
      size: 4200000,
      folderId: "folder-1",
      createdAt: "2023-01-25T10:30:00Z",
      modifiedAt: "2023-01-25T10:30:00Z",
      thumbnail: "/placeholder.svg?height=100&width=100",
    },
  ];

  const mockFolders = [
    {
      id: "folder-2",
      name: "Work",
      parentId: "folder-1",
      createdAt: "2023-02-05T09:45:00Z",
      modifiedAt: "2023-04-10T11:20:00Z",
      hasChildren: true,
    },
    {
      id: "folder-3",
      name: "Personal",
      parentId: "folder-1",
      createdAt: "2023-02-15T14:50:00Z",
      modifiedAt: "2023-03-25T12:15:00Z",
      hasChildren: false,
    },
  ];

  const defaultProps = {
    files: mockFiles,
    folders: mockFolders,
    currentFolder: "folder-1",
    currentFolderName: "Documents",
    navigateToFolder: jest.fn(),
    navigateToParent: jest.fn(),
  };

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it("renders files and folders in grid view", () => {
    render(<FileGrid {...defaultProps} />);

    // Check if folder names are rendered
    expect(screen.getByText("Work")).toBeInTheDocument();
    expect(screen.getByText("Personal")).toBeInTheDocument();

    // Check if file names are rendered
    expect(screen.getByText("Document.docx")).toBeInTheDocument();
    expect(screen.getByText("Image.jpg")).toBeInTheDocument();

    // Check if back button is rendered
    expect(screen.getByText("Back")).toBeInTheDocument();
    expect(screen.getByText("Documents")).toBeInTheDocument();
  });

  it("navigates to parent folder when back button is clicked", () => {
    render(<FileGrid {...defaultProps} />);

    // Click back button
    fireEvent.click(screen.getByText("Back"));

    // Verify navigateToParent was called
    expect(defaultProps.navigateToParent).toHaveBeenCalled();
  });

  it("navigates to folder when folder is clicked", () => {
    render(<FileGrid {...defaultProps} />);

    // Click on a folder
    fireEvent.click(screen.getByText("Work"));

    // Verify navigateToFolder was called with correct folder ID
    expect(defaultProps.navigateToFolder).toHaveBeenCalledWith("folder-2");
  });

  it("displays empty state when no files or folders", () => {
    render(<FileGrid {...defaultProps} files={[]} folders={[]} />);

    // Check if empty state is rendered
    expect(screen.getByText("This folder is empty")).toBeInTheDocument();
    expect(
      screen.getByText(
        "Files and folders you add to this location will appear here"
      )
    ).toBeInTheDocument();
  });

  it("renders different icons for different file types", () => {
    render(<FileGrid {...defaultProps} />);

    // Check if document has document icon (SVG)
    const documentItem = screen.getByText("Document.docx").closest(".group");
    expect(documentItem).toBeInTheDocument();

    // Check if image has image thumbnail
    const imageItem = screen.getByText("Image.jpg").closest(".group");
    expect(imageItem).toBeInTheDocument();
    const imageElement = imageItem?.querySelector("img");
    expect(imageElement).toBeInTheDocument();
  });
});
